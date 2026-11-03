// 1. تلوين وإلغاء تلوين الخيارات اليومية (مثل الصلاة، المشاعر، إلخ)
function toggleHobby(el) {
    el.classList.toggle('selected-hobby');
}

// 2. إرسال دعوة الوالدين في صفحة parent.html
function sendInvitation() {
    let email = document.getElementById('parent-email').value;
    if(email.trim() !== "") {
        document.getElementById('invite-step-1').style.display = 'none';
        document.getElementById('invite-success').style.display = 'block';
    } else {
        alert('Please enter a valid email address ✍️');
    }
}

// 3. التفاعل مع نقاط أيام الأسبوع في صفحة الكرات الخارقة powers.html
document.addEventListener('click', function(e) {
    if(e.target.classList && e.target.classList.contains('dot')) {
        e.target.classList.toggle('active-dot');
    }
});

// 4. حفظ إجابة سؤال اليوم في الصفحة الرئيسية
function submitSmileAnswer() {
    alert("🚀 Leafy received your answer and kept it safe!");
}