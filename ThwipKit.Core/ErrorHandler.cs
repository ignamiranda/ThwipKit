using System;
using System.IO;

namespace ThwipKit.Core
{
    public enum ErrorSeverity
    {
        Info,
        Warning,
        Error
    }

    public enum ErrorRecoveryOption
    {
        None,
        Retry,
        Skip,
        Cancel
    }

    public class ToolError
    {
        public string Code { get; init; } = "";
        public string UserMessage { get; init; } = "";
        public string? TechnicalDetail { get; init; }
        public ErrorSeverity Severity { get; init; } = ErrorSeverity.Error;
        public ErrorRecoveryOption[] RecoveryOptions { get; init; } = Array.Empty<ErrorRecoveryOption>();
        public Exception? Exception { get; init; }

        public string FullMessage => string.IsNullOrEmpty(TechnicalDetail)
            ? UserMessage
            : $"{UserMessage}\n\nTechnical detail: {TechnicalDetail}";
    }

    public static class ErrorHandler
    {
        public static ToolError FileNotFound(string filePath, string fileType = "file")
        {
            return new ToolError
            {
                Code = "ERR_FILE_NOT_FOUND",
                UserMessage = $"The {fileType} could not be found:\n{filePath}\n\nPlease check the path and try again.",
                Severity = ErrorSeverity.Error,
                RecoveryOptions = new[] { ErrorRecoveryOption.Retry, ErrorRecoveryOption.Cancel }
            };
        }

        public static ToolError AccessDenied(string path)
        {
            return new ToolError
            {
                Code = "ERR_ACCESS_DENIED",
                UserMessage = $"Access denied to:\n{path}\n\nThe file may be in use by another program, or you may need administrator privileges.",
                Severity = ErrorSeverity.Error,
                RecoveryOptions = new[] { ErrorRecoveryOption.Retry, ErrorRecoveryOption.Cancel }
            };
        }

        public static ToolError InvalidGamePath(string path)
        {
            return new ToolError
            {
                Code = "ERR_INVALID_GAME_PATH",
                UserMessage = $"The specified path does not appear to be a valid Spider-Man Remastered installation:\n{path}\n\nA valid installation should contain an 'asset_archive' folder with a 'TOC' file.",
                Severity = ErrorSeverity.Error,
                RecoveryOptions = new[] { ErrorRecoveryOption.Retry, ErrorRecoveryOption.Cancel }
            };
        }

        public static ToolError TextureNotFound(string textureName)
        {
            return new ToolError
            {
                Code = "ERR_TEXTURE_NOT_FOUND",
                UserMessage = $"Texture '{textureName}' was not found in the game archives.\n\nPlease verify the texture name and try again.",
                Severity = ErrorSeverity.Error,
                RecoveryOptions = new[] { ErrorRecoveryOption.Retry, ErrorRecoveryOption.Cancel }
            };
        }

        public static ToolError InvalidPngFile(string filePath, string reason = "")
        {
            string message = $"The file is not a valid PNG image:\n{filePath}";
            if (!string.IsNullOrEmpty(reason))
            {
                message += $"\n\nReason: {reason}";
            }
            return new ToolError
            {
                Code = "ERR_INVALID_PNG",
                UserMessage = message,
                Severity = ErrorSeverity.Error,
                RecoveryOptions = new[] { ErrorRecoveryOption.Retry, ErrorRecoveryOption.Cancel }
            };
        }

        public static ToolError ArchiveCorrupted(string archivePath)
        {
            return new ToolError
            {
                Code = "ERR_ARCHIVE_CORRUPTED",
                UserMessage = $"The game archive appears to be corrupted:\n{archivePath}\n\nIf you have a backup, try restoring it. Otherwise, verify game files through Steam or Epic Games.",
                Severity = ErrorSeverity.Error,
                RecoveryOptions = new[] { ErrorRecoveryOption.Cancel }
            };
        }

        public static ToolError BackupFailed(string textureName, string reason)
        {
            return new ToolError
            {
                Code = "ERR_BACKUP_FAILED",
                UserMessage = $"Failed to create backup for texture '{textureName}'.\n\nReason: {reason}\n\nThe operation was aborted to protect your game files.",
                Severity = ErrorSeverity.Warning,
                RecoveryOptions = new[] { ErrorRecoveryOption.Retry, ErrorRecoveryOption.Cancel }
            };
        }

        public static ToolError ConversionFailed(string fromFormat, string toFormat, string? detail = null)
        {
            return new ToolError
            {
                Code = "ERR_CONVERSION_FAILED",
                UserMessage = $"Failed to convert from {fromFormat} to {toFormat}.\n\nThe texture data may be in an unsupported format.",
                TechnicalDetail = detail,
                Severity = ErrorSeverity.Error,
                RecoveryOptions = new[] { ErrorRecoveryOption.Cancel }
            };
        }

        public static ToolError ExternalToolNotFound(string toolName)
        {
            return new ToolError
            {
                Code = "ERR_EXTERNAL_TOOL_MISSING",
                UserMessage = $"Required external tool '{toolName}' was not found.\n\nPlease ensure the tool is installed and in your system PATH, or configure its location in settings.",
                Severity = ErrorSeverity.Error,
                RecoveryOptions = new[] { ErrorRecoveryOption.Cancel }
            };
        }

        public static ToolError FromException(Exception ex, string context = "")
        {
            string code = ex switch
            {
                FileNotFoundException => "ERR_FILE_NOT_FOUND",
                UnauthorizedAccessException => "ERR_ACCESS_DENIED",
                DirectoryNotFoundException => "ERR_DIR_NOT_FOUND",
                IOException => "ERR_IO",
                ArgumentException => "ERR_INVALID_ARG",
                _ => "ERR_UNKNOWN"
            };

            return new ToolError
            {
                Code = code,
                UserMessage = string.IsNullOrEmpty(context)
                    ? ex.Message
                    : $"{context}: {ex.Message}",
                TechnicalDetail = ex.StackTrace,
                Severity = ErrorSeverity.Error,
                Exception = ex,
                RecoveryOptions = new[] { ErrorRecoveryOption.Retry, ErrorRecoveryOption.Cancel }
            };
        }
    }

    public static class PngValidator
    {
        private static readonly byte[] PngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        public static bool IsValidPng(string filePath)
        {
            if (!File.Exists(filePath)) return false;

            try
            {
                using var stream = File.OpenRead(filePath);
                byte[] header = new byte[8];
                int read = stream.Read(header, 0, 8);
                if (read < 8) return false;

                for (int i = 0; i < 8; i++)
                {
                    if (header[i] != PngSignature[i]) return false;
                }

                // Read IHDR chunk for dimensions
                stream.Seek(8, SeekOrigin.Begin);
                byte[] lengthBytes = new byte[4];
                if (stream.Read(lengthBytes, 0, 4) < 4) return false;

                byte[] chunkType = new byte[4];
                if (stream.Read(chunkType, 0, 4) < 4) return false;

                if (chunkType[0] != 0x49 || chunkType[1] != 0x48 ||
                    chunkType[2] != 0x44 || chunkType[3] != 0x52)
                {
                    return false;
                }

                return true;
            }
            catch
            {
                return false;
            }
        }

        public static (int width, int height)? GetDimensions(string filePath)
        {
            if (!IsValidPng(filePath)) return null;

            try
            {
                using var stream = File.OpenRead(filePath);
                stream.Seek(16, SeekOrigin.Begin);

                byte[] widthBytes = new byte[4];
                byte[] heightBytes = new byte[4];

                if (stream.Read(widthBytes, 0, 4) < 4) return null;
                if (stream.Read(heightBytes, 0, 4) < 4) return null;

                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(widthBytes);
                    Array.Reverse(heightBytes);
                }

                int width = BitConverter.ToInt32(widthBytes, 0);
                int height = BitConverter.ToInt32(heightBytes, 0);

                return (width, height);
            }
            catch
            {
                return null;
            }
        }

        public static bool ValidateForRebuild(string filePath, out ToolError? error)
        {
            error = null;

            if (!File.Exists(filePath))
            {
                error = ErrorHandler.FileNotFound(filePath, "PNG file");
                return false;
            }

            if (!IsValidPng(filePath))
            {
                error = ErrorHandler.InvalidPngFile(filePath, "File does not have a valid PNG signature.");
                return false;
            }

            var dimensions = GetDimensions(filePath);
            if (dimensions == null)
            {
                error = ErrorHandler.InvalidPngFile(filePath, "Could not read PNG dimensions.");
                return false;
            }

            if (dimensions.Value.width <= 0 || dimensions.Value.height <= 0)
            {
                error = ErrorHandler.InvalidPngFile(filePath,
                    $"Invalid dimensions: {dimensions.Value.width}x{dimensions.Value.height}");
                return false;
            }

            if (dimensions.Value.width > 8192 || dimensions.Value.height > 8192)
            {
                error = ErrorHandler.InvalidPngFile(filePath,
                    $"Dimensions too large: {dimensions.Value.width}x{dimensions.Value.height}. Maximum supported: 8192x8192.");
                return false;
            }

            return true;
        }
    }
}