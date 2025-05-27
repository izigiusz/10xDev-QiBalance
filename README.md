# QiBalance

## Table of Contents

- [Project Description](#project-description)
- [Tech Stack](#tech-stack)
- [Getting Started Locally](#getting-started-locally)
- [Available Scripts](#available-scripts)
- [Project Scope](#project-scope)
- [Project Status](#project-status)
- [License](#license)

## Project Description

QiBalance is a full-stack web application designed to help users balance their energy and optimize their overall well-being. The application provides an intuitive interface built using Blazor WebApp and leverages AI functionalities through Semantic Kernel for dynamic insights and recommendations. With robust backend support provided by Supabase and a focus on modern visual design using Blazor.Bootstrap components, QiBalance aims to deliver an engaging and efficient user experience.

## Tech Stack

- **Frontend:** Blazor WebApp (.NET 9) with Interactive Server Render Mode, utilizing Blazor.Bootstrap for accessible UI components.
- **Backend:** 
  - Supabase as a comprehensive backend solution offering a PostgreSQL database, built-in user authentication, and multi-language SDK support.
- **AI:** Integration with OpenAI using the Semantic Kernel library for enhanced interactive communication.
- **CI/CD & Hosting:** 
  - Github Actions for continuous integration and deployment.
  - MonsterAsp for application hosting on a global scale.

## Getting Started Locally

1. **Clone the repository:**
   ```bash
   git clone https://github.com/yourusername/QiBalance.git
   cd QiBalance
   ```

2. **Prerequisites:**
   - .NET 9 SDK
   - Supabase account and necessary credentials for local setup.

3. **Setup Environment Variables:**
   - Create a `.env` file in the root directory and add required environment variables (e.g., Supabase URL, Supabase Key, OpenAI API key).

4. **Install Dependencies:**
   - Restore .NET packages:
     ```bash
     dotnet restore
     ```
 

5. **Run the Application:**
   ```bash
   dotnet run
   ```
   The application should start on [http://localhost:5000](http://localhost:5000) by default.

## Available Scripts

- **Starting the application:**
  ```bash
  dotnet run
  ```
- **Running Tests:**
  ```bash
  dotnet test
  ```
- **Building the application:**
  ```bash
  dotnet build
  ```

*(If additional scripts are available, please add them accordingly.)*

## Project Scope

The QiBalance project aims to deliver the following key functionalities:
- User authentication and profile management via Supabase.
- Real-time data processing and display using Blazor WebApp.
- AI-driven insights and recommendations powered by OpenAI (via Semantic Kernel).
- A responsive and accessible user interface leveraging Blazor.Bootstrap.
- A scalable architecture ready for future enhancements and increased user demand.

## Project Status

The project is currently in the development phase with a focus on delivering a Minimum Viable Product (MVP). Future iterations will include additional features and performance optimizations based on user feedback and testing.

## License

This project is licensed under the [MIT License](LICENSE). 