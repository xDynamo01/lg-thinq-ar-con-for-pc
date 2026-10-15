using System.IO;
using System.Security.Cryptography;
using System.Text;
namespace ThinQDesktop;
static class AccountStore
{
    static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ThinQDesktop");
    static string FilePath => Path.Combine(Folder, "account.bin");
    public static string? Load()
    {
        if (!File.Exists(FilePath)) return null;
        var bytes = ProtectedData.Unprotect(File.ReadAllBytes(FilePath), null, DataProtectionScope.CurrentUser);
        try { return Encoding.UTF8.GetString(bytes); } finally { CryptographicOperations.ZeroMemory(bytes); }
    }
    public static string Country => File.Exists(Path.Combine(Folder,"country.txt")) ? File.ReadAllText(Path.Combine(Folder,"country.txt")).Trim() : "BR";
    public static void Save(string token, string country)
    {
        Directory.CreateDirectory(Folder);
        var bytes = Encoding.UTF8.GetBytes(token);
        try { File.WriteAllBytes(FilePath, ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser)); }
        finally { CryptographicOperations.ZeroMemory(bytes); }
        File.WriteAllText(Path.Combine(Folder,"country.txt"), country);
    }
    public static void Forget() { if (File.Exists(FilePath)) File.Delete(FilePath); }
}
