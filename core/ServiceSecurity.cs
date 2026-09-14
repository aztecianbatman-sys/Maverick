using System.Security.AccessControl;
using System.Security.Principal;

namespace Maverick.Core;

public static class ServiceSecurity
{
    public static void ProtectLocalStorage(CorePaths paths)
    {
        Directory.CreateDirectory(paths.RootDirectory);
        Directory.CreateDirectory(paths.QuarantineDirectory);

        SetAcl(paths.RootDirectory, allowAuthenticatedRead: false);
        SetAcl(paths.QuarantineDirectory, allowAuthenticatedRead: false);
    }

    private static void SetAcl(string path, bool allowAuthenticatedRead)
    {
        var directory = new DirectoryInfo(path);
        var acl = directory.GetAccessControl();

        acl.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);

        acl.SetAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.SystemSid, null),
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));

        acl.SetAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            FileSystemRights.FullControl,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None,
            AccessControlType.Allow));

        if (allowAuthenticatedRead)
        {
            acl.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null),
                FileSystemRights.ReadAndExecute,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
        }

        directory.SetAccessControl(acl);
    }
}
