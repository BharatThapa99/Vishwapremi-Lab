# Vishwapremi Lab — C# source
Shree Vishwapremi Secondary School, Arun 5, Yaku, Bhojpur. Designed by Bharat Thapa.

Version 1.1 fixes local-network connection failures from Public Windows network profiles, filters inactive adapters, and adds certificate-pinned automatic teacher discovery on UDP 8765. The encrypted controller remains on TCP 8766. Firewall rules accept local-subnet traffic only.

The school uses the compiled installers, not this source folder. No development tools are required on school PCs. Read Quick-Start.html for deployment and limitations.

## Build
Developer machine: Windows x64, .NET SDK 10, Inno Setup 6.7 or newer. These tools are only for rebuilding the software.

```powershell
.\Build.ps1 -InnoCompiler 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe'
```

This runs the integration checks, publishes both self-contained single-file applications, and creates installers under `installers`. Runtime security fixes require republishing with an updated SDK/runtime and redistributing new installers. Update Third-Party-Notices.txt when updating the runtime.

## Structure
- Teacher: WinForms interface, local state and Kestrel HTTPS endpoints on port 8766.
- Student: WinForms/tray connection, certificate-pinned HTTPS client and a fixed allowlist of commands.
- Shared: models, persistent store, Windows DPAPI credential storage and native UI helpers.
- Tests: integration checks for authentication, pairing, persistence, expiry, acknowledgements and pinned TLS.

```powershell
dotnet run --project Tests\Tests.csproj -c Release -- --tests
```

Running the Tests executable without arguments opens a development preview using temporary sample data. It also creates a temporary QA student identity inside its test-data directory and sends one test notice if that identity connects. This is not included in either school installer. The production application never seeds sample computers or bypasses teacher sign-in.

## Security and operational boundaries
- Teacher password: salted PBKDF2-SHA256, 210,000 iterations. Dashboard access is native/local only.
- Device pairing: 256-bit one-use enrollment token, 24-hour expiry, certificate fingerprint transferred in the pairing file. Enrollment issues a separate device token. Only token hashes are stored by the teacher.
- Network: HTTPS with a local self-signed certificate pinned by the student. No system-wide certificate trust changes. Certificate private key and student token are DPAPI-protected for the Windows user.
- Actions: only notices, HTTP/HTTPS website launch, and Windows lock. No shell or arbitrary program execution API. Commands expire after 60 seconds. Student records an action before invoking it to avoid executing it again after a crash; interruptions can therefore yield a failed action instead of a retry.
- Closing a window keeps the application running in the tray. Exiting ends the connection. The student is intentionally a user-session application, not a tamper-resistant service. It starts after Windows sign-in, not at boot.
- Teacher state is a JSON file in that Windows user's local app-data folder; writes use replacement. This is not an adversarial multi-user authorization boundary. Users who control the teacher Windows account control the controller's local state.
- Every teacher using this release uses the same app password and designated Windows account. Per-teacher accounts, remote dashboard access, screen viewing, enforcement policies and internet/game blocking are future work.
- Controller identity expires after five years. Plan renewal and re-pairing before then. Backup is tied to the Windows user; a new host/account needs a new controller and pairing.
- Installers are unsigned. Signing requires a publisher-owned code-signing certificate, which is not included.

## Release testing
Automated local tests and UI inspection are not a substitute for testing on the school's actual network. Deploy to two PCs first. No real student screens, router settings or school machines are touched by the tests. The Windows lock action is not invoked on the development host during QA.
