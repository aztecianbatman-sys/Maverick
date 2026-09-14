using System.Security.AccessControl;
using System.Security.Principal;

namespace Maverick.Core;

public static class ServiceSecurity
{
    private const string ServiceName = "Maverick Core";

    public static void ProtectLocalStorage(CorePaths paths)
    {
        Directory.CreateDirectory(paths.RootDirectory);
        Directory.CreateDirectory(paths.QuarantineDirectory);

        SetDirectoryAcl(paths.RootDirectory);
        SetDirectoryAcl(paths.QuarantineDirectory);
    }

    private static void SetDirectoryAcl(string path)
    {
        var directory = new DirectoryInfo(path);
        var acl = directory.GetAccessControl();

        acl.SetAccessRuleProtection(true, false);

        acl.SetAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
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

        try
        {
            var serviceSid = new NTAccount("NT SERVICE", ServiceName)
                .Translate(typeof(SecurityIdentifier));

            acl.SetAccessRule(new FileSystemAccessRule(
                serviceSid,
                FileSystemRights.ReadAndExecute |
                FileSystemRights.Write |
                FileSystemRights.CreateFiles |
                FileSystemRights.CreateDirectories |
                FileSystemRights.Delete,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.None,
                AccessControlType.Allow));
        }
        catch
        {
            // SCM assigns the service SID during installation.
        }

        directory.SetAccessControl(acl);
    }
}
