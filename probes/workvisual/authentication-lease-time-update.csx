using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using KukaRoboter.OnlineServicesFacade;

var address = "198.51.100.134";
var remotePath = @"C:\KRC\SmartHMI\Config\Authentication.config";
var remoteBackupPath = @"C:\KRC\SmartHMI\Config\Authentication.config.codex-backup-TASK-20260829";
var evidenceDirectory = @"C:\Users\PUBLIC_TEST_USER\Desktop\工作项目文件\contracts开发\artifacts\kuka-lab\authentication-lease-time\WP5L-20260831-LEASE-ZERO-LIVE-01";
var localBackupPath = Path.Combine(evidenceDirectory, "Authentication.config.pre-change.backup");
var candidatePath = Path.Combine(evidenceDirectory, "Authentication.config.lease-zero.candidate");
var readbackPath = Path.Combine(evidenceDirectory, "Authentication.config.post-change.readback");
var remoteBackupReadbackPath = Path.Combine(evidenceDirectory, "Authentication.config.remote-backup.readback");
var restoreReadbackPath = Path.Combine(evidenceDirectory, "Authentication.config.restore.readback");
var oldBytes = Encoding.UTF8.GetBytes("<LeaseTime>300</LeaseTime>");
var newBytes = Encoding.UTF8.GetBytes("<LeaseTime>0</LeaseTime>");

string Sha256(byte[] bytes)
{
    using (var hash = SHA256.Create())
    {
        return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", string.Empty);
    }
}

int Count(byte[] source, byte[] pattern)
{
    var count = 0;
    for (var index = 0; index <= source.Length - pattern.Length; index++)
    {
        var matched = true;
        for (var offset = 0; offset < pattern.Length; offset++)
        {
            if (source[index + offset] != pattern[offset])
            {
                matched = false;
                break;
            }
        }
        if (matched)
        {
            count++;
            index += pattern.Length - 1;
        }
    }
    return count;
}

byte[] ReplaceAll(byte[] source, byte[] oldPattern, byte[] newPattern)
{
    using (var output = new MemoryStream())
    {
        for (var index = 0; index < source.Length;)
        {
            var matched = index <= source.Length - oldPattern.Length;
            for (var offset = 0; matched && offset < oldPattern.Length; offset++)
            {
                matched = source[index + offset] == oldPattern[offset];
            }
            if (matched)
            {
                output.Write(newPattern, 0, newPattern.Length);
                index += oldPattern.Length;
            }
            else
            {
                output.WriteByte(source[index]);
                index++;
            }
        }
        return output.ToArray();
    }
}

Directory.CreateDirectory(evidenceDirectory);
foreach (var path in new[] { localBackupPath, candidatePath, readbackPath, remoteBackupReadbackPath, restoreReadbackPath })
{
    if (File.Exists(path))
    {
        Logger.Error("AUTH_LEASE_UPDATE_FAILED: evidence path already exists: " + path);
        return 41;
    }
}

try
{
    using (var repository = new FileHandlingFacade(address))
    {
        repository.Download(remotePath, localBackupPath);
        var original = File.ReadAllBytes(localBackupPath);
        var originalHash = Sha256(original);
        Logger.Info("REMOTE_PATH=" + remotePath);
        Logger.Info("REMOTE_BACKUP_PATH=" + remoteBackupPath);
        Logger.Info("LOCAL_BACKUP_PATH=" + localBackupPath);
        Logger.Info("ORIGINAL_SHA256=" + originalHash);
        Logger.Info("ORIGINAL_LENGTH=" + original.Length);
        Logger.Info("ORIGINAL_LEASE_300_COUNT=" + Count(original, oldBytes));
        Logger.Info("ORIGINAL_LEASE_0_COUNT=" + Count(original, newBytes));

        if (Count(original, oldBytes) != 2 || Count(original, newBytes) != 0)
        {
            Logger.Error("AUTH_LEASE_UPDATE_FAILED: original file did not contain exactly two 300 values and zero 0 values.");
            return 42;
        }

        if (!repository.FileExists(remoteBackupPath))
        {
            repository.Upload(localBackupPath, remoteBackupPath, false);
        }
        repository.Download(remoteBackupPath, remoteBackupReadbackPath);
        var remoteBackup = File.ReadAllBytes(remoteBackupReadbackPath);
        var remoteBackupHash = Sha256(remoteBackup);
        Logger.Info("REMOTE_BACKUP_SHA256=" + remoteBackupHash);
        if (!string.Equals(originalHash, remoteBackupHash, System.StringComparison.OrdinalIgnoreCase))
        {
            Logger.Error("AUTH_LEASE_UPDATE_FAILED: remote backup hash does not match original.");
            return 43;
        }

        var candidate = ReplaceAll(original, oldBytes, newBytes);
        if (Count(candidate, oldBytes) != 0 || Count(candidate, newBytes) != 2)
        {
            Logger.Error("AUTH_LEASE_UPDATE_FAILED: candidate replacement count is not exact.");
            return 44;
        }
        var reversed = ReplaceAll(candidate, newBytes, oldBytes);
        if (!original.SequenceEqual(reversed))
        {
            Logger.Error("AUTH_LEASE_UPDATE_FAILED: candidate contains changes outside the two exact replacements.");
            return 45;
        }

        File.WriteAllBytes(candidatePath, candidate);
        var candidateHash = Sha256(candidate);
        Logger.Info("CANDIDATE_SHA256=" + candidateHash);
        Logger.Info("CANDIDATE_LENGTH=" + candidate.Length);

        repository.Upload(candidatePath, remotePath, true);
        repository.Download(remotePath, readbackPath);
        var readback = File.ReadAllBytes(readbackPath);
        var readbackHash = Sha256(readback);
        Logger.Info("READBACK_SHA256=" + readbackHash);
        Logger.Info("READBACK_LEASE_300_COUNT=" + Count(readback, oldBytes));
        Logger.Info("READBACK_LEASE_0_COUNT=" + Count(readback, newBytes));

        if (!string.Equals(candidateHash, readbackHash, System.StringComparison.OrdinalIgnoreCase)
            || Count(readback, oldBytes) != 0
            || Count(readback, newBytes) != 2)
        {
            Logger.Error("AUTH_LEASE_UPDATE_FAILED: controller readback did not match candidate; restoring original.");
            repository.Upload(localBackupPath, remotePath, true);
            repository.Download(remotePath, restoreReadbackPath);
            var restoredHash = Sha256(File.ReadAllBytes(restoreReadbackPath));
            Logger.Info("RESTORE_READBACK_SHA256=" + restoredHash);
            Logger.Info("RESTORE_VERIFIED=" + string.Equals(originalHash, restoredHash, System.StringComparison.OrdinalIgnoreCase));
            return 46;
        }

        Logger.Info("AUTH_LEASE_UPDATE_VERIFIED=true");
        return 0;
    }
}
catch (System.Exception exception)
{
    Logger.Error("AUTH_LEASE_UPDATE_FAILED: " + exception.GetType().FullName + ": " + exception.GetBaseException().Message);
    return 47;
}
