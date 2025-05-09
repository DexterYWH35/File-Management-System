# File-Management-System
A website that allows users to perform CRUD to handle Files.

# Description

This is a File Management System built using ASP.NET Core MVC, designed to allow users to securely store, manage, and access their files. The system includes role-based access control (RBAC), where regular users can manage their own files, while an admin has full access to all files.

# Features

1. User Authentication & Authorization

2. Users register with an email and password (default role: User)

3. Only one Admin exists in the system

4. Users can log in using their email and manage their profile

5. Users can create folders and store files into different folder for a more structured way to manage their files 

# Role-Based Access

1. Users can upload, view, update, and delete only their own files

2. Admin can manage all files in the system

3. File Management

4. Upload and store files locally

5. View a list of uploaded files

6. Download, update, and delete files


# Profile Management

Users can update their full name & email


# Installation & Setup

Prerequisites

Ensure you have the following installed:

1. .NET 9.0

2. SQL Server

3. Visual Studio Code or Visual Studio

# Future Improvements

1. Implement cloud storage (AWS S3) //Hosted on AWS but since its need to pay, I will be shutting it down and only reboot for display purpose.

2. Add file sharing functionality

3. Implement an advanced search feature



<img width="1440" alt="Screenshot 2025-05-05 at 5 58 56 PM" src="https://github.com/user-attachments/assets/68300952-070d-4c40-90ef-7e9f3f2a5846" />



# Remarks
1. Change Program.cs make sure listening to http://0.0.0.0:5000
2. Change the appsetting.json for publish folder, make sure pointing to the right database (under/var/www/projectFolder)
3. Run the code using "dotnet run" at project folder. X inside publish folder.
