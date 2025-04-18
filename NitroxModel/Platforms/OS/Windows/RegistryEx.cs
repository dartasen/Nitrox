using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace NitroxModel.Platforms.OS.Windows;

#if NET5_0_OR_GREATER
[System.Runtime.Versioning.SupportedOSPlatform("windows")]
#endif
public static class RegistryEx
{
    /// <summary>
    ///     Reads the value of the registry key or returns the default value of <see cref="T" />.
    /// </summary>
    /// <param name="pathWithValue">
    ///     Full path to the registry key. If the registry hive is omitted then "current user" is used.
    /// </param>
    /// <param name="defaultValue">The default value if the registry key is not found or failed to convert to <see cref="T" />.</param>
    /// <typeparam name="T">Type of value to read. If the value in the registry key does not match it will try to convert.</typeparam>
    /// <returns>Value as read from registry or null if not found.</returns>
    public static T Read<T>(string pathWithValue, T defaultValue = default)
    {
        (RegistryKey baseKey, string valueKey) = GetKey(pathWithValue, false);
        if (baseKey == null)
        {
            return defaultValue;
        }

        try
        {
            object value = baseKey.GetValue(valueKey);
            if (value == null)
            {
                return defaultValue;
            }
            Type typeOfT = typeof(T);
            return value.GetType() == typeOfT ? (T)value : (T)TypeDescriptor.GetConverter(typeOfT).ConvertFrom(value);
        }
        catch (Exception)
        {
            return defaultValue;
        }
        finally
        {
            baseKey.Dispose();
        }
    }

    /// <summary>
    ///     Deletes the whole subtree or value, whichever exists.
    /// </summary>
    /// <param name="pathWithOptionalValue">If no value name is given it will delete the key instead.</param>
    /// <returns>True if something was deleted.</returns>
    public static bool Delete(string pathWithOptionalValue)
    {
        (RegistryKey key, string valueKey) = GetKey(pathWithOptionalValue);
        if (key == null)
        {
            return false;
        }

        // Try to delete the key.
        RegistryKey prev = key;
        key = key.OpenSubKey(valueKey);
        if (key != null)
        {
            key.DeleteSubKeyTree(valueKey);
            key.Dispose();
            prev.Dispose();
            return true;
        }
        key = prev; // Restore state for next step

        // Not a key, delete the value if it exists.
        if (key.GetValue(valueKey) != null)
        {
            key.DeleteValue(valueKey);
            key.Dispose();
            return true;
        }

        // Nothing to delete.
        return false;
    }

    public static void Write<T>(string pathWithKey, T value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }
        (RegistryKey baseKey, string valueKey) = GetKey(pathWithKey, true, true);
        if (baseKey == null)
        {
            return;
        }

        // Figure out what kind of value to store.
        RegistryValueKind? kind = value switch
        {
            int => RegistryValueKind.DWord,
            long => RegistryValueKind.QWord,
            byte[] => RegistryValueKind.Binary,
            string => RegistryValueKind.String,
            _ => null
        };
        // If regKey already exists, and we don't know how to parse the value, use existing kind.
        if (!kind.HasValue)
        {
            try
            {
                kind = baseKey.GetValueKind(valueKey);
            }
            catch (Exception)
            {
                // ignored - thrown when key does not exist.
            }
        }

        try
        {
            baseKey.SetValue(valueKey, value, kind.GetValueOrDefault(RegistryValueKind.String));
        }
        finally
        {
            baseKey.Dispose();
        }
    }

    /// <summary>
    ///     Checks if a key exists.
    /// </summary>
    public static bool Exists(string pathWithValue)
    {
        (RegistryKey baseKey, string valueKey) = GetKey(pathWithValue, false);
        if (baseKey == null)
        {
            return false;
        }

        try
        {
            return baseKey.GetValue(valueKey) != null;
        }
        finally
        {
            baseKey.Dispose();
        }
    }

    /// <summary>
    ///     Waits for a registry value to have the given value.
    /// </summary>
    public static async Task CompareWaitAsync<T>(string pathWithKey, Func<T, bool> predicate, CancellationToken token)
    {
        CancellationTokenSource innerCts = null;
        if (token == default)
        {
            innerCts = new(TimeSpan.FromSeconds(10));
            token = innerCts.Token;
        }

        RegistryKey baseKey = null;
        try
        {
            // Test once before in-case it is already successful.
            (baseKey, string valueKey) = GetKey(pathWithKey, false);
            if (Test(baseKey, valueKey, predicate))
            {
                return;
            }

            // Wait for predicate to be successful.
            while (true)
            {
                token.ThrowIfCancellationRequested();
                // If regkey didn't exist yet it might later.
                if (baseKey == null)
                {
                    (baseKey, valueKey) = GetKey(pathWithKey, false);
                }

                if (Test(baseKey, valueKey, predicate))
                {
                    break;
                }
                await Task.Delay(100, token);
            }
        }
        finally
        {
            baseKey?.Dispose();
            innerCts?.Dispose();
        }

        static bool Test(RegistryKey regKey, string regKeyName, Func<T, bool> testPredicate)
        {
            T preTestVal = regKey?.GetValue(regKeyName) is T typedValue ? typedValue : default;
            return testPredicate(preTestVal);
        }
    }

    public static Task CompareWaitAsync<T>(string pathWithKey, Func<T, bool> predicate, TimeSpan timeout = default)
    {
        CancellationTokenSource source = new(timeout == default ? TimeSpan.FromSeconds(10) : timeout);
        return CompareWaitAsync(pathWithKey, predicate, source.Token);
    }

    private static (RegistryKey baseKey, string valueKey) GetKey(string path, bool needsWriteAccess = true, bool createIfNotExists = false)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return (null, null);
        }
        path = path.Trim();

        // Parse path to get the registry key instance.
        Span<string> parts = path.Split(Path.DirectorySeparatorChar);
        Span<string> partsWithoutHive;
        RegistryHive hive = RegistryHive.CurrentUser;
        string regPathWithoutHiveOrKey;
        if (!parts[0].Equals("Computer", StringComparison.OrdinalIgnoreCase))
        {
            partsWithoutHive = parts[..^1];
            regPathWithoutHiveOrKey = string.Join(Path.DirectorySeparatorChar.ToString(), partsWithoutHive.ToArray());
        }
        else
        {
            partsWithoutHive = parts[2..^1];
            regPathWithoutHiveOrKey = string.Join(Path.DirectorySeparatorChar.ToString(), partsWithoutHive.ToArray());
            hive = parts[1].ToLower() switch
            {
                "hkey_classes_root" => RegistryHive.ClassesRoot,
                "hkey_local_machine" => RegistryHive.LocalMachine,
                "hkey_current_user" => RegistryHive.CurrentUser,
                "hkey_users" => RegistryHive.Users,
                "hkey_current_config" => RegistryHive.CurrentConfig,
                _ => throw new ArgumentException($"Path must contain a valid registry hive but was given '{parts[1]}'", nameof(path))
            };
        }

        RegistryKey hiveRef = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
        RegistryKey key = hiveRef.OpenSubKey(regPathWithoutHiveOrKey, needsWriteAccess);
        // Should the key (and its path leading to it) be created?
        if (key == null && createIfNotExists)
        {
            key = hiveRef;
            foreach (string part in partsWithoutHive)
            {
                RegistryKey prev = key;
                try
                {
                    key = key?.OpenSubKey(part, needsWriteAccess) ?? key?.CreateSubKey(part, needsWriteAccess);
                }
                finally
                {
                    // Cleanup old/parent key reference
                    prev?.Dispose();
                }
            }
        }

        return (key, parts[^1]);
    }
}
