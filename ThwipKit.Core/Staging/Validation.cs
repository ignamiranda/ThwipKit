using System;
using System.IO;
using ThwipKit.Core.Editors;
using ThwipKit.Core.Games;

namespace ThwipKit.Core.Staging;

public sealed class ProgressReporter
{
    private readonly Action<string, int, int>? _progressCallback;
    private int _currentProgress;
    private int _totalSteps;
    private string _currentOperation = string.Empty;

    public ProgressReporter(Action<string, int, int>? progressCallback = null)
    {
        _progressCallback = progressCallback;
    }

    public void StartOperation(string operationName, int totalSteps)
    {
        _currentOperation = operationName;
        _totalSteps = totalSteps;
        _currentProgress = 0;
        ReportProgress();
    }

    public void UpdateProgress(int stepsCompleted)
    {
        _currentProgress = Math.Min(stepsCompleted, _totalSteps);
        ReportProgress();
    }

    public void IncrementProgress()
    {
        _currentProgress = Math.Min(_currentProgress + 1, _totalSteps);
        ReportProgress();
    }

    public void CompleteOperation()
    {
        _currentProgress = _totalSteps;
        ReportProgress();
    }

    public void ReportMessage(string message)
    {
        _progressCallback?.Invoke(message, _currentProgress, _totalSteps);
    }

    private void ReportProgress()
    {
        _progressCallback?.Invoke(_currentOperation, _currentProgress, _totalSteps);
    }
}

public sealed class AssetValidator
{
    private readonly GameBase _game;

    public AssetValidator(GameBase game)
    {
        _game = game ?? throw new ArgumentNullException(nameof(game));
    }

    public ValidationResult ValidateAsset(string filePath)
    {
        if (!File.Exists(filePath))
        {
            return ValidationResult.Failure($"File not found: {filePath}");
        }

        try
        {
            if (new FileInfo(filePath).Length == 0)
            {
                return ValidationResult.Failure("File is empty");
            }

            return ValidationResult.Success();
        }
        catch (Exception ex)
        {
            return ValidationResult.Failure(ex.Message);
        }
    }

    public ValidationResult ValidateAssetForReplacement(string filePath, ulong targetAssetId, string gamePath)
    {
        ValidationResult result = ValidateAsset(filePath);
        if (!result.IsValid)
        {
            return result;
        }

        try
        {
            long size = new FileInfo(filePath).Length;
            if (size > int.MaxValue)
            {
                result.Errors.Add($"Replacement file too large ({size} bytes)");
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add(ex.Message);
        }

        return result;
    }
}
