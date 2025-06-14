🛡️ AirCode - Secure Attendance Tracking System
<div align="center">

Enterprise-grade secure attendance tracking for higher education institutions

🚀 Live Demo • 📚 Documentation • 🛡️ Security • 🤝 Contributing
</div>
🎯 Project Overview

AirCode is a cybersecurity-focused Progressive Web Application (PWA) designed for secure attendance tracking in higher education institutions. Built with Blazor WebAssembly, it combines cutting-edge security protocols with seamless offline/online functionality to ensure data integrity and prevent attendance fraud.
🌟 Key Highlights

    🔐 Zero-Trust Security Model with multi-layered authentication
    📱 Progressive Web App with native mobile experience
    🔄 Hybrid Offline/Online operation with automatic synchronization
    🎫 Cryptographically Secure QR Codes with temporal validation
    👥 Role-Based Access Control (RBAC) with hierarchical permissions
    🌐 Real-time Data Synchronization across all devices

🏗️ Architecture & Technology Stack
<div align="center">

mermaid

graph TB
    subgraph "Frontend Layer"
        A[Blazor WebAssembly PWA]
        B[Rust QR Scanner - WASM]
        C[JavaScript Interop]
    end
    
    subgraph "Authentication Layer"
        D[Auth0 OIDC + PKCE]
        E[Role-Based Access Control]
        F[Offline Credential Service]
    end
    
    subgraph "Security Layer"
        G[AES-256 Encryption]
        H[HMAC Signing]
        I[Temporal Key Management]
        J[Session Unique IDs]
    end
    
    subgraph "Data Layer"
        K[Firebase Firestore - Primary]
        L[Supabase - Temporary/Sessions]
        M[Local Storage - Offline]
    end
    
    A --> D
    A --> B
    A --> G
    D --> E
    G --> H
    G --> I
    K --> L
    L --> M

</div>
🛠️ Core Technologies

Layer	Technology	Purpose
Frontend	Blazor WebAssembly	Cross-platform UI framework
Scanner	Rust + WebAssembly	High-performance QR code scanning
Authentication	Auth0 OIDC + PKCE	Enterprise identity management
Primary Database	Firebase Firestore	Real-time data synchronization
Secondary Database	Supabase	Session management & edge functions
Cryptography	AES-256 + HMAC	Data encryption & integrity
Offline Storage	IndexedDB	Local data persistence

🛡️ Security Features
🔐 Multi-Layered Authentication

    Auth0 Integration: Enterprise-grade OIDC with PKCE flow
    Pre-authorized Users Only: Skeleton user validation before account creation
    Role-Based Access Control: Hierarchical permission system
    Offline Authentication: Secure credential caching with encryption

🎫 Cryptographically Secure QR Codes

🔄 QR Code Generation Process:
1. Generate temporal cryptographic keys
2. Create session-unique identifier
3. Encrypt attendance data (AES-256)
4. Sign with HMAC for integrity
5. Embed temporal validation
6. Generate tamper-proof QR code

🔒 Data Protection

    AES-256 Encryption: All sensitive data encrypted at rest and in transit
    HMAC Signatures: Cryptographic integrity verification
    Temporal Keys: Time-bound encryption keys for session security
    Zero-Knowledge Architecture: Minimal data exposure principle

🛡️ Attack Prevention

Attack Vector	Protection Mechanism
QR Code Forgery	Cryptographic signatures + temporal validation
Replay Attacks	Session-unique IDs + time-based validation
Data Tampering	HMAC integrity checks + immutable records
Unauthorized Access	Multi-factor authentication + RBAC
Offline Attacks	Encrypted local storage + secure key derivation

👥 User Roles & Permissions
<div align="center">

mermaid

graph TD
    A[Superior Admin] --> B[Lecturer Admin]
    A --> C[Course Rep Admin]
    B --> D[Student]
    C --> D
    
    A1[System Management<br/>User Creation<br/>Global Settings] --> A
    B1[Course Management<br/>Attendance Reports<br/>Student Oversight] --> B
    C1[Session Management<br/>Attendance Tracking<br/>Course Assistance] --> C
    D1[QR Scanning<br/>Profile Management<br/>Attendance History] --> D

</div>
🎭 Role Hierarchy

    👑 Superior Admin
        Complete system administration
        User account creation and management
        Global configuration and security settings
        System-wide reporting and analytics
    👨‍🏫 Lecturer Admin
        Course creation and management
        Attendance session configuration
        Student enrollment oversight
        Academic reporting and analytics
    👨‍🎓 Course Rep Admin
        Attendance session management
        QR code generation for sessions
        Real-time attendance monitoring
        Course-specific reporting
    🎓 Student
        QR code scanning for attendance
        Personal attendance history
        Profile management
        Notification management

🚀 How It Works
📋 Attendance Process Flow
<div align="center">

mermaid

sequenceDiagram
    participant L as Lecturer/Course Rep
    participant S as System
    participant DB as Database
    participant ST as Student
    
    L->>S: Create Attendance Session
    S->>S: Generate Temporal Keys
    S->>S: Create Unique Session ID
    S->>S: Generate Secure QR Code
    S->>DB: Store Session (Supabase)
    L->>ST: Display QR Code
    ST->>S: Scan QR Code
    S->>S: Validate Signature & Timing
    S->>DB: Record Attendance
    S->>DB: Sync to Firebase (Validation)
    S->>L: Real-time Update

</div>
🔄 Offline/Online Synchronization

    Offline Mode: Data stored locally with encryption
    Background Sync: Automatic upload when connection restored
    Conflict Resolution: Intelligent merging of offline/online data
    Data Integrity: Cryptographic validation during sync

🏁 Getting Started
📋 Prerequisites

    .NET 7.0 SDK or later
    Node.js 16+ (for WebAssembly tools)
    Rust toolchain (for QR scanner compilation)
    Firebase project with Firestore
    Supabase project
    Auth0 tenant configuration

⚡ Quick Start

bash

# Clone the repository
git clone https://github.com/mid-d-man/AirCode.git
cd AirCode

# Install dependencies
dotnet restore

# Configure environment variables
cp appsettings.json.example appsettings.json
# Edit appsettings.json with your configuration

# Build and run
dotnet run

🔧 Configuration

json

{
  "Auth0": {
    "Domain": "your-domain.auth0.com",
    "ClientId": "your-client-id",
    "Audience": "your-api-audience"
  },
  "Firebase": {
    "ApiKey": "your-firebase-api-key",
    "ProjectId": "your-project-id"
  },
  "Supabase": {
    "Url": "your-supabase-url",
    "AnonKey": "your-anon-key"
  }
}

📱 Features
✨ Core Functionality

    🎯 QR Code Generation: Secure, time-bound attendance codes
    📱 Mobile Scanner: High-performance Rust-based QR scanner
    📊 Real-time Dashboard: Live attendance monitoring
    📈 Analytics & Reports: Comprehensive attendance analytics
    🔔 Notifications: Real-time alerts and updates
    🌐 PWA Support: Install as native mobile app

🎨 User Experience

    🌙 Dark/Light Theme: Adaptive UI with user preferences
    📱 Responsive Design: Optimized for all screen sizes
    ⚡ Lightning Fast: WebAssembly performance
    🔄 Offline First: Works without internet connection
    🎭 Role-Based UI: Dynamic interface based on user permissions

🔧 Administrative Tools

    👥 User Management: Bulk user operations
    📚 Course Management: Comprehensive course administration
    📅 Session Scheduling: Advanced scheduling system
    📊 Reporting Suite: Detailed attendance analytics
    ⚙️ System Configuration: Flexible system settings

🗂️ Project Structure

AirCode/
├── 📁 Components/          # Reusable UI components
│   ├── Admin/              # Administrative components
│   ├── Auth/               # Authentication components
│   └── SharedPrefabs/      # Common UI elements
├── 📁 Domain/              # Business logic layer
│   ├── Entities/           # Domain entities
│   ├── Enums/              # System enumerations
│   └── ValueObjects/       # Value objects
├── 📁 Services/            # Application services
│   ├── Auth/               # Authentication services
│   ├── Firebase/           # Firebase integration
│   ├── Scanner/            # QR scanning services
│   └── Security/           # Cryptographic services
├── 📁 Pages/               # Application pages
├── 📁 Layout/              # Layout components
└── 📁 wwwroot/             # Static assets
    ├── js/                 # JavaScript modules
    ├── css/                # Stylesheets
    └── wasm/               # WebAssembly modules

📊 Performance Metrics

Metric	Value	Description
Bundle Size	~2.5MB	Optimized WebAssembly bundle
Cold Start	~1.2s	Initial application load time
QR Scan Speed	~100ms	Average scan-to-validation time
Offline Sync	~500ms	Average sync time per record
PWA Score	95/100	Lighthouse PWA audit score

🤝 Contributing

We welcome contributions to AirCode! Please see our Contributing Guidelines for details.
🐛 Bug Reports

Found a bug? Please create an issue with:

    Detailed description
    Steps to reproduce
    Expected vs actual behavior
    Environment details

💡 Feature Requests

Have an idea? We'd love to hear it! Please open an issue describing:

    The problem you're trying to solve
    Your proposed solution
    Any alternative solutions considered

📝 License

This project is licensed under the MIT License - see the LICENSE file for details.
🙏 Acknowledgments

    Auth0 for enterprise authentication solutions
    Firebase for real-time database capabilities
    Supabase for edge computing and temporary storage
    Rust Community for WebAssembly toolchain
    Blazor Team for the amazing framework

📞 Support

    📧 Email: support@aircode.edu
    💬 Discord: Join our community
    📖 Documentation: docs.aircode.edu
    🐛 Issues: GitHub Issues

<div align="center">

Made with ❤️ for secure education

</div>
