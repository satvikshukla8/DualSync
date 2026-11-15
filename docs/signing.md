# Signing and Windows download warnings

The alpha 6 DualSync executable is **unsigned**. The application icon and ZIP checksum do not establish publisher identity. The optional VB-CABLE package has its own vendor signatures, which the setup helper verifies; those signatures do not sign DualSync.

For broader distribution, use a consistent publisher identity backed by a trusted code-signing provider. Microsoft documents [Artifact Signing](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation) as an option for distribution outside the Store. Enrollment requires identity validation. No signing account, paid service, or certificate has been created for this project.

Signing identifies the publisher and protects the integrity of signed files. A newly signed download can still trigger a reputation warning. Microsoft's [SmartScreen guidance](https://learn.microsoft.com/en-us/windows/apps/package-and-deploy/smartscreen-reputation) explains the distinction between publisher reputation and file reputation; its [Smart App Control guidance](https://learn.microsoft.com/en-us/windows/apps/develop/smart-app-control/code-signing-for-smart-app-control) describes trusted signatures for protected devices. Policies on some PCs may block this unsigned alpha.

Keep Windows security enabled. Test distribution on the target Windows configuration and record the actual result. If a security policy prevents the app from running, use a suitably signed build rather than asking testers to disable the policy.

When signing is configured, sign the DualSync executable and project-owned assemblies after publishing, verify the signatures, and then create the ZIP and checksum. Preserve vendor signatures on external components. Never commit private signing keys, access tokens, or certificate passwords.
