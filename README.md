# GH Vault 🛡️

**GH Vault** is a secure, centralized web application developed to manage corporate network and cybersecurity devices. It helps IT teams securely store device credentials, track IP architectures, and manage department-based access authorizations.

## 🚀 Project Overview
Managing credentials, IP addresses, and metadata for hundreds of enterprise devices (Firewalls, Switches, Load Balancers) via spreadsheets is risky and inefficient. GH Vault solves this problem by providing a secure, encrypted, and easy-to-use web dashboard. 

Developed with a security-first approach, it features role-based access and multi-tenant department isolation, meaning Network and Cyber Security teams can only view and manage their respective infrastructure.

## ✨ Key Features
* **Secure Storage:** Device passwords are encrypted with AES-256 before being saved to the database.
* **Active Directory (LDAP):** Corporate users can easily log in using their existing Windows credentials.
* **Role-Based Access Control (RBAC):** Hierarchical access levels (SuperAdmin, Operator, Viewer) ensure strict authorization.
* **Smart Excel Import/Export:** Easily batch-add devices via Excel. The system automatically detects and skips duplicate IP/Hostname entries.
* **Dashboard & Analytics:** Visualizes device distribution across vendors and departments in real-time.

## 💻 Technologies Used

**Backend:**
* C# / .NET Core Web API
* Microsoft SQL Server (MSSQL)
* Entity Framework Core
* AES-256 Encryption & JWT Authentication

**Frontend:**
* HTML5, CSS3, Vanilla JavaScript
* Bootstrap 5 (Dark & Light Theme)
* DataTables.js, Chart.js, SweetAlert2
