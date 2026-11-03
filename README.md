#  Growia

An AI-powered platform that helps parents understand their children and strengthen their weak points, through a fun, game-like experience the child enjoys.

Children use an engaging app (daily journal, tasks, badges, levels, and a friendly character), while the data they naturally generate is analyzed by AI. Parents get clear insights about their child's strengths and weaknesses, and personalized tips to support them, without the child feeling monitored.

---

# Features

# For Children
- Daily journal and daily tasks
- Badges, challenges, and levels to keep motivation high
- "Leafy", a friendly companion character that gives tips and encouragement
- Hero profile and journey pages

# For Parents
- Parent dashboard with an overview of each child's progress
- AI-generated insights on strengths and weak points
- Child history and activity tracking
- Notifications and alerts
- Personalized recommendations to help improve weak areas

# AI Analysis
- NLP analysis of the child's journal entries
- Personality clustering of children
- Trust score and pillar-based evaluation
- Weekly badge recommendations
- Daily and periodic AI orchestration jobs

# Admin
- Admin dashboard for managing the system

---

# Tech Stack

| Layer | Technology |
|-------|------------|
| Backend | ASP.NET Core Web API (C#) |
| Database | SQL Server + Entity Framework Core |
| Authentication | JWT |
| Security | AES encryption for sensitive data |
| Frontend | HTML, CSS, JavaScript ,React|
| AI | NLP + clustering services (integrated through the API) |
| Email | SMTP (Mailtrap for development) |

---

# Project Structure

```
graduation-project/
├── backend/     # ASP.NET Core Web API
│   ├── Controllers/
│   ├── Services/
│   ├── Models/
│   ├── DTOs/
│   ├── Migrations/
│   └── appsettings.json
└── frontend/    # HTML / CSS / JS pages
```

---

# Getting Started

# Prerequisites
- [.NET SDK](https://dotnet.microsoft.com/download) (match the version in `GrawiaaApp.API.csproj`)
- SQL Server (LocalDB or full)
- VS Code with the **Live Server** extension (or any static server)

1. Backend

```bash
cd backend

# Create your local settings file (copy appsettings.json and fill in your values)
copy appsettings.json appsettings.Development.json

# Apply database migrations
dotnet ef database update

# Run the API
dotnet run
```

Fill in these values in `appsettings.Development.json`:
- `Jwt:Key`
- `ConnectionStrings:DefaultConnection`
- `Encryption:Key` (32 characters) and `Encryption:IV` (16 characters)
- `EmailSettings` (SMTP credentials)
- `AiTeam:ApiKey`

2. Frontend

Open the `frontend` folder in VS Code, then right-click `index.html` → **Open with Live Server**
(it runs on `http://127.0.0.1:5500` by default).

Make sure the API URL inside the frontend scripts matches the port your backend is running on.



#  Privacy

Sensitive data is encrypted, and child identifiers are hashed using a privacy-safe approach before being used in analysis.

---

# Team

- Eng Eman,Eng Sara : Backend 
- Eng Menna, Eng Amira, Eng Norhan :Frontend 
- Eng Somia :Ai

---

#  License

This project was developed as a graduation project.
