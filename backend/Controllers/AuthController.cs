using GrawiaaApp.API.Data;
using GrawiaaApp.API.DTOs;
using GrawiaaApp.API.Interfaces;
using GrawiaaApp.API.Models;
using GrawiaaApp.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace GrawiaaApp.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly ApplicationDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IEmailSender _emailSender;
        private readonly ICacheService _cache;
        private readonly IDefaultTaskSeederService _taskSeeder;
        private readonly IChildIdentityService _childIdentity;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IConfiguration configuration,
            ApplicationDbContext context,
            IHttpContextAccessor httpContextAccessor,
            IEmailSender emailSender,
            ICacheService cache,
            IDefaultTaskSeederService taskSeeder,
            IChildIdentityService childIdentity)
        {
            _userManager = userManager;
            _configuration = configuration;
            _signInManager = signInManager;
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _emailSender = emailSender;
            _cache = cache;
            _taskSeeder = taskSeeder;
            _childIdentity = childIdentity;
        }

        [HttpPost("admin-login")]
        public async Task<IActionResult> AdminLogin([FromBody] AdminLoginDto dto)
        {
            try
            {
                var user = await _userManager.FindByNameAsync(dto.Username);
                if (user == null) return Unauthorized(new { message = "Invalid username or password" });

                var roles = await _userManager.GetRolesAsync(user);
                if (!roles.Any(r => r.Equals("Admin", StringComparison.OrdinalIgnoreCase)))
                {
                    if (user.UserType == "Admin")
                        await _userManager.AddToRoleAsync(user, "Admin");
                    else
                        return Unauthorized(new { message = "Sorry, this account does not have admin privileges" });
                }

                var isPasswordValid = await _userManager.CheckPasswordAsync(user, dto.Password);
                if (!isPasswordValid) return Unauthorized(new { message = "Invalid username or password" });

                user.LastActiveAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);

                var token = await GenerateJwtToken(user);
                return Ok(new { token, username = user.UserName, role = "Admin" });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Internal error", detail = ex.Message }); }
        }

        [HttpPost("register/gradient")]
        public async Task<IActionResult> RegisterGradient([FromBody] ParentSignUpDto model)
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).ToList();
                return BadRequest(new { message = "Invalid data", errors });
            }

            var userExists = await _userManager.FindByEmailAsync(model.Email);
            if (userExists != null) return BadRequest(new { message = "This email is already registered" });

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.Name,
                UserType = "Parent",
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
                return BadRequest(new { message = "Invalid data", errors = result.Errors.Select(e => e.Description).ToList() });

            await _userManager.AddToRoleAsync(user, "Parent");

            _context.ParentProfiles.Add(new ParentProfile { UserId = user.Id });
            await _context.SaveChangesAsync();

            _cache.RemoveByPrefix(CacheKeys.AdminPrefix);

            return Ok(new { message = "Your garden has been created successfully! 🌻" });
        }

        [HttpPost("login/parent")]
        public async Task<IActionResult> ParentLogin([FromBody] LoginDto model)
        {
            try
            {
                var user = await _userManager.Users
                    .FirstOrDefaultAsync(u => u.Email == model.UsernameOrEmail || u.UserName == model.UsernameOrEmail);

                if (user == null || !await _userManager.CheckPasswordAsync(user, model.Password))
                    return Unauthorized(new { message = "Invalid email or password" });

                if (user.UserType != "Parent")
                    return Unauthorized(new { message = "Sorry, this account is not a parent account" });

                // Ensure the user actually has the Parent role assigned in AspNetUserRoles.
                // Older accounts created before seeding may be missing the role assignment.
                var roles = await _userManager.GetRolesAsync(user);
                if (!roles.Contains("Parent"))
                    await _userManager.AddToRoleAsync(user, "Parent");

                user.LastActiveAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);

                var token = await GenerateJwtToken(user);

                return Ok(new
                {
                    token,
                    fullName = user.FullName,
                    role = user.UserType,
                    userId = user.Id
                });
            }
            catch (Exception ex) { return StatusCode(500, new { message = "Error during login", detail = ex.Message }); }
        }

        [AllowAnonymous]
        [HttpGet("device-children")]
        public async Task<IActionResult> GetDeviceChildren()
        {
            var fingerprint = GetCurrentDeviceFingerprint();

            var children = await _cache.GetOrCreateAsync($"device:{fingerprint}:children", async () =>
                await _context.ChildProfiles
                    .Where(p => p.DeviceFingerprint == fingerprint)
                    .Select(p => new { username = p.User.UserName, fullName = p.User.FullName, characterType = p.CharacterType })
                    .ToListAsync()
            , TimeSpan.FromMinutes(10));

            return Ok(children);
        }

        [HttpPost("signup-child")]
        public async Task<IActionResult> SignupChild([FromBody] ChildSignupDto dto)
        {
            var existingUser = await _userManager.FindByNameAsync(dto.Username);
            if (existingUser != null)
                return BadRequest(new { message = "This username is already taken by another hero, try a different one! 🦸" });

            if (string.IsNullOrWhiteSpace(dto.ParentEmail) && string.IsNullOrWhiteSpace(dto.ChildPersonalEmail))
                return BadRequest(new { message = "You must enter either your personal email or a parent email for linking!" });

            if (!ModelState.IsValid) return BadRequest(ModelState);

            string accountEmail = !string.IsNullOrWhiteSpace(dto.ChildPersonalEmail)
                ? dto.ChildPersonalEmail
                : $"{dto.Username}@leafy.internal";

            var childUser = new ApplicationUser
            {
                UserName = dto.Username,
                Email = accountEmail,
                FullName = dto.FullName,
                EmailConfirmed = true,
                UserType = "Child",
                LastActiveAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(childUser, dto.Password);
            if (!result.Succeeded) return BadRequest(result.Errors);

            await _userManager.AddToRoleAsync(childUser, "Child");

            var fingerprint = GetCurrentDeviceFingerprint();

            var childProfile = new ChildProfile
            {
                UserId = childUser.Id,
                Age = dto.Age,
                CharacterName = dto.CharacterName ?? dto.CharacterType,
                CharacterType = dto.CharacterType,
                TrustScore = 100,
                Level = 1,
                IsStandalone = true,
                PendingLinkToken = Guid.NewGuid().ToString("N").Substring(0, 6).ToUpper(),
                DeviceFingerprint = fingerprint,
                LastKnownIp = _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString(),
            };

            bool isLinked = false;
            string parentName = "";

            if (!string.IsNullOrWhiteSpace(dto.ParentEmail))
            {
                var pEmail = dto.ParentEmail.Trim();
                childProfile.PendingParentEmail = pEmail;
                var parentUser = await _userManager.Users.FirstOrDefaultAsync(u => u.Email == pEmail && u.UserType == "Parent");

                if (parentUser != null)
                {
                    childProfile.ParentId = parentUser.Id;
                    childUser.ParentId = parentUser.Id;
                    childProfile.IsStandalone = false;
                    childProfile.PendingLinkToken = null;
                    childProfile.PendingParentEmail = null;
                    isLinked = true;
                    parentName = parentUser.FullName ?? "Parent";
                    await _userManager.UpdateAsync(childUser);

                    _cache.RemoveByPrefix(CacheKeys.ParentPrefix(parentUser.Id));
                }
            }

            _context.ChildProfiles.Add(childProfile);

            // ── Seed fixed recurring tasks for this child ─────────────
            // Adds the default journal tasks (prayers, Quran, etc.) so the
            // AI team's Anomaly Detection engine always finds a baseline
            // task list for every child, even before the parent assigns any.
            var hashedId = _childIdentity.GetSafeChildKey(childUser.Id);
            _taskSeeder.SeedFixedTasksForChild(childUser.Id, hashedId, _context);

            if (dto.Hobbies != null && dto.Hobbies.Any())
            {
                foreach (var hobbyName in dto.Hobbies)
                {
                    var hobby = await _context.Hobbies.FirstOrDefaultAsync(h => h.Name == hobbyName);
                    if (hobby != null)
                        _context.ChildHobbies.Add(new ChildHobby { ChildId = childUser.Id, HobbyId = hobby.Id });
                }
            }

            await _context.SaveChangesAsync();

            _cache.RemoveByPrefix(CacheKeys.AdminPrefix);
            _cache.Remove($"device:{fingerprint}:children");

            var childToken = await GenerateJwtToken(childUser);
            return Ok(new { token = childToken, message = "Hero account created successfully! 🎉", isLinked, parentName = isLinked ? parentName : "Not linked to a parent yet" });
        }

        [HttpPost("reset-child-password-final")]
        public async Task<IActionResult> CompleteChildPasswordReset([FromBody] CompleteResetDto model)
        {
            var user = await _userManager.FindByNameAsync(model.UserName);
            if (user == null) return NotFound(new { message = "Hero not found" });

            var profile = await _context.ChildProfiles
                .FirstOrDefaultAsync(p => p.UserId == user.Id && p.ResetToken == model.Code && p.PasswordResetPending == true);

            if (profile == null) return BadRequest(new { message = "Invalid code or reset not requested! ❌" });

            var removeResult = await _userManager.RemovePasswordAsync(user);
            if (!removeResult.Succeeded)
                return BadRequest(new { message = "Failed to remove old password" });

            var addResult = await _userManager.AddPasswordAsync(user, model.NewPassword);
            if (!addResult.Succeeded)
                return BadRequest(new { message = addResult.Errors.First().Description });

            profile.PasswordResetPending = false;
            profile.ResetToken = null;

            var claims = await _userManager.GetClaimsAsync(user);
            var resetClaim = claims.FirstOrDefault(c => c.Type == "PasswordResetRequired");
            if (resetClaim != null)
                await _userManager.RemoveClaimAsync(user, resetClaim);

            await _context.SaveChangesAsync();

            var newToken = await GenerateJwtToken(user);
            return Ok(new { token = newToken, message = "Password changed successfully! 🎉" });
        }

        [HttpPost("login-child")]
        public async Task<IActionResult> ChildLogin([FromBody] ChildLoginRequest request)
        {
            if (string.IsNullOrEmpty(request.Username))
                return BadRequest(new { message = "Please enter your name, hero! 🦸" });

            var user = await _userManager.FindByNameAsync(request.Username);
            if (user == null) return Unauthorized(new { message = "Hero not found! 🕵️" });

            var roles = await _userManager.GetRolesAsync(user);
            if (!roles.Contains("Child"))
            {
                // Account exists with UserType=Child but missing role assignment — fix it automatically
                if (user.UserType == "Child")
                    await _userManager.AddToRoleAsync(user, "Child");
                else
                    return Unauthorized(new { message = "This area is only for young heroes! 🛡️" });
            }

            var claims = await _userManager.GetClaimsAsync(user);
            bool resetRequired = claims.Any(c => c.Type == "PasswordResetRequired");

            if (resetRequired)
            {
                var resetToken = await GenerateJwtToken(user);
                return Ok(new
                {
                    token = resetToken,
                    username = user.UserName,
                    passwordResetRequired = true,
                    message = "Time to change your secret key, hero! 🔑"
                });
            }

            var isPasswordValid = await _userManager.CheckPasswordAsync(user, request.Password);
            if (!isPasswordValid) return Unauthorized(new { message = "Wrong password.. try again! ❌" });

            user.LastActiveAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            var childProfile2 = await _context.ChildProfiles.FirstOrDefaultAsync(p => p.UserId == user.Id);
            if (childProfile2 != null)
            {
                var fp = GetCurrentDeviceFingerprint();
                childProfile2.DeviceFingerprint = fp;
                await _context.SaveChangesAsync();
                _cache.Remove($"device:{fp}:children");
            }

            var childToken = await GenerateJwtToken(user);
            return Ok(new
            {
                token = childToken,
                userId = user.Id,
                username = user.UserName,
                fullName = user.FullName,
                passwordResetRequired = false
            });
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto model)
        {
            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null) return NotFound(new { message = "Email not registered" });

            if (user.UserType == "Child" && user.Email != null && user.Email.EndsWith("@leafy.internal"))
                return BadRequest(new { message = "Hero, your account is managed by your parent..." });

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            var frontendUrl = _configuration["FrontendUrl"] ?? "http://127.0.0.1:5500/growia-fixed";
            var resetLink =
     $"{frontendUrl}/growia_frontend/reset-password.html?email={Uri.EscapeDataString(user.Email ?? "")}&token={Uri.EscapeDataString(token)}&type={user.UserType.ToLower()}";
            var emailBody = $@"<h2>Growia Password Reset</h2><p>Hi {user.FullName}, click the link below to reset your magic password:</p><a href='{resetLink}'>Reset Password</a>";

            await _emailSender.SendEmailAsync(user.Email!, "Reset Your Password", emailBody);
            return Ok(new { message = "Magic link sent! Check your inbox 📧" });
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto model)
        {
            if (!ModelState.IsValid)
            {
                var error = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage;
                return BadRequest(new { message = error ?? "Invalid data" });
            }

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user == null) return BadRequest(new { message = "Invalid Request" });

            var result = await _userManager.ResetPasswordAsync(user, model.Token, model.NewPassword);
            if (result.Succeeded) return Ok(new { message = "Password reset successful! ✅" });

            return BadRequest(new { message = "Link expired or invalid. Please request a new one." });
        }

        private string GetCurrentDeviceFingerprint()
        {
            var request = _httpContextAccessor.HttpContext?.Request;
            var staticDeviceId = request?.Headers["X-Device-Fingerprint"].ToString();
            // FIX: Only trust the explicit X-Device-Fingerprint header sent by the client.
            // The previous fallback (first 16 chars of User-Agent) was not a real fingerprint —
            // it matched across millions of devices with the same browser/OS prefix, causing
            // GetDeviceChildren to return incorrect results. Return an empty string instead;
            // the caller will receive an empty list, which is the correct safe default.
            return !string.IsNullOrEmpty(staticDeviceId) ? staticDeviceId : string.Empty;
        }

        private async Task<string> GenerateJwtToken(ApplicationUser user)
        {
            var keyString = _configuration["Jwt:Key"];
            if (string.IsNullOrEmpty(keyString)) throw new Exception("JWT Key is missing in appsettings.json");

            var key = Encoding.UTF8.GetBytes(keyString);
            var roles = await _userManager.GetRolesAsync(user);

            var claims = new List<Claim>
    {
        new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        new Claim("unique_name", user.UserName!),
        new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
        new Claim(ClaimTypes.Name, user.UserName!),
        new Claim("username", user.UserName!),
        new Claim("UserType", user.UserType ?? "Admin")
    };

            // 🟢 التعديل الصحيح هنا: بنلف على الأدوار وبنضيف الـ Claim الرسمي فقط
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role)); // سيبنا السطر الرسمي ده وحذفنا "role" الصغيرة
            }

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                notBefore: DateTime.UtcNow,
                expires: DateTime.UtcNow.AddHours(12),
                signingCredentials: new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256)
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}