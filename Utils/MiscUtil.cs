using System.IO;

namespace OpennessCopy.Utils;

public static class MiscUtil
{
    public static string SanitizeFileName(string fileName)
    {
        char[] invalidChars = Path.GetInvalidFileNameChars();
        foreach (char c in invalidChars)
        {
            fileName = fileName.Replace(c, '`');
        }

        // Also replace '/' specifically (it might not be in GetInvalidFileNameChars on all systems)
        fileName = fileName.Replace('/', '`');
        fileName = fileName.Replace('\\', '`');

        return fileName;
    }
    
    public static System.Security.SecureString ConvertToSecureString(string password)
    {
        if (string.IsNullOrEmpty(password)) return null;

        var secureString = new System.Security.SecureString();
        foreach (char c in password)
        {
            secureString.AppendChar(c);
        }
        secureString.MakeReadOnly();
        return secureString;
    }
}