# Security

Shellmason is a personal project by Thomas Schueler, built on personal time and equipment. See [OWNERSHIP.md](OWNERSHIP.md).

## Reporting a vulnerability

Do not open a public GitHub issue for a security report.

Email [thomas@schuelertech.com](mailto:thomas@schuelertech.com) with:

- The Shellmason version from Help, About, and whether you ran an installed copy or a build from source
- What you observed, and the command you were building if it is safe to share
- No passwords, tokens, or text from a credential dialog

Shellmason runs PowerShell you choose, including an elevated process after a UAC prompt, and it can install a missing module only after you choose Yes. A report is still useful when the app mishandles a secret: the preview, the output pane, the command line, or a script file.

There is no bug-bounty program.
