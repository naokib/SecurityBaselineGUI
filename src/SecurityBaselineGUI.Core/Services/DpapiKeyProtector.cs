using System.Security.Cryptography;

namespace SecurityBaselineGUI.Core.Services;

/// <summary>
/// 仕様書7.2: DBの暗号化キーをWindows DPAPI(CurrentUserスコープ)で保護する。
/// キー本体はディスクに平文で置かず、DPAPI保護済みblobのみを保存する。
/// CurrentUserスコープを選ぶ理由は、ドメインアカウントのDPAPIマスターキーが
/// ドメインコントローラーにバックアップされ、ハードウェア故障時の別マシンへの
/// 移行時にも同一ドメインアカウントであれば復号できるようにするため
/// (仕様書7.2/7.4)。ワークグループ環境では移行時の復元ができない点に注意。
///
/// DBファイル自体はSQLCipherで暗号化し、本クラスはその生鍵をDPAPIで保護する。
/// </summary>
public sealed class DpapiKeyProtector
{
    private const int KeySizeBytes = 32;

    public byte[] LoadOrCreateProtectedKey(string protectedKeyFilePath)
    {
        if (File.Exists(protectedKeyFilePath))
        {
            var protectedBytes = File.ReadAllBytes(protectedKeyFilePath);
            return ProtectedData.Unprotect(protectedBytes, optionalEntropy: null, DataProtectionScope.CurrentUser);
        }

        var key = RandomNumberGenerator.GetBytes(KeySizeBytes);
        var protectedKey = ProtectedData.Protect(key, optionalEntropy: null, DataProtectionScope.CurrentUser);

        var directory = Path.GetDirectoryName(protectedKeyFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
        File.WriteAllBytes(protectedKeyFilePath, protectedKey);

        return key;
    }
}
