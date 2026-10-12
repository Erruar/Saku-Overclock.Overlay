using System.Buffers;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Saku_Overclock.Overlay.Contracts;
using Saku_Overclock.Overlay.Helpers;
using Saku_Overclock.Shared;
using Saku_Overclock.Shared.Models;

namespace Saku_Overclock.Overlay.Services;

public partial class RtssService(IRtssSettingsService settings, ICpuGateService cpuService, IApplyerGateService applyer, ILogger<RtssService> logger) : IRtssService
{
    private bool _allowUpdate = true;
    private bool _isRtssAvailable = true;
    private bool _isTemplateCreated;
    private string _cachedEditorText = string.Empty;
    private string _coreTemplate = string.Empty;
    private string _compactSizing = string.Empty;
    private int _startIndex = -1;
    private int _endIndex = -1;
    private string _appliedPreset = string.Empty;
    private int? _coreCount;

    public void Enable()
    {
        if (_allowUpdate) return;
        _allowUpdate = true;
        applyer.PresetChanged += OnPresetChanged;
        _ = LoadInitialPresetAsync();
    } 
    
    public void Disable()
    {
        if (!_allowUpdate) return;
        applyer.PresetChanged -= OnPresetChanged;
        RtssHandler.ResetOsdText();
        _isTemplateCreated = false;
        _allowUpdate = false;
    }
    
    private void OnPresetChanged(Preset preset)
    {
        _appliedPreset = preset.PresetName;
    }
    
    private async Task LoadInitialPresetAsync()
    {
        try
        {
            var name = await applyer.GetAppliedPreset();
            if (!string.IsNullOrEmpty(name))
                _appliedPreset = name;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load initial preset");
        }
    }
    
    private void UpdateTemplateCache(string? editorText)
    {
        _cachedEditorText = editorText ?? string.Empty;

        if (string.IsNullOrEmpty(_cachedEditorText))
        {
            _startIndex = -1;
            _endIndex = -1;
            _coreTemplate = string.Empty;
            _compactSizing = string.Empty;
            return;
        }

        _startIndex = _cachedEditorText.IndexOf("$cpu_clock_cycle$", StringComparison.Ordinal);
        _endIndex = _cachedEditorText.IndexOf("$cpu_clock_cycle_end$", StringComparison.Ordinal);

        var match = ClockCycleRegex().Match(_cachedEditorText);
        _coreTemplate = match is { Success: true, Groups.Count: > 1 } ? match.Groups[1].Value : string.Empty;

        if (!string.IsNullOrEmpty(_coreTemplate))
        {
            _compactSizing = "<Br><S0>е" + (_coreTemplate.Contains("<S1>") ? "<S1>" : string.Empty);
        }
        else
        {
            _compactSizing = string.Empty;
        }
    }
    
    public void UpdateRtss(SensorsInformation sensorsInformation)
    {
        if (!_isRtssAvailable || !_allowUpdate) return;
        
        try
        {
            if (!_isTemplateCreated)
            {
                UpdateTemplateCache(settings.AdvancedCodeEditor);
                _isTemplateCreated = true;
            }
            
            _coreCount = (int)cpuService.Cores;

            if (string.IsNullOrEmpty(_cachedEditorText))
            {
                logger.LogWarning("AdvancedCodeEditor string was empty");
                return;
            }

            ProcessAndSendRtssTemplate(sensorsInformation);
        }
        catch (DllNotFoundException)
        {
            _isRtssAvailable = false;
            logger.LogWarning("SakuRTSSCLI.dll was not found. RTSS integration was disabled untill reboot");
        }
        catch (Exception ex)
        {
            logger.LogError("Exception when updating RTSS metrics: {Exception}", ex);
        }
    }

    private void ProcessAndSendRtssTemplate(SensorsInformation sensorsInformation)
    {
        if (_startIndex == -1 || _endIndex == -1 || _endIndex <= _startIndex + 17)
        {
            ProcessSimpleTemplate(sensorsInformation);
            return;
        }

        try
        {
            ProcessComplexTemplate(sensorsInformation);
        }
        catch (DllNotFoundException)
        {
            throw; 
        }
        catch (Exception ex)
        {
            logger.LogWarning("Failed to parse RTSS template: {ExMessage}", ex.Message);
            ProcessSimpleTemplate(sensorsInformation);
        }
    }

    private void ProcessSimpleTemplate(SensorsInformation sensorsInformation)
    {
        var estimatedLength = EstimateResultLength(_cachedEditorText);
        var buffer = ArrayPool<char>.Shared.Rent(estimatedLength);

        try
        {
            var length = ReplaceAllPlaceholders(_cachedEditorText.AsSpan(), buffer, sensorsInformation);
            RtssHandler.ChangeOsdTextSpan(buffer.AsSpan(0, length));
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    private void ProcessComplexTemplate(SensorsInformation sensorsInformation)
    {
        var cores = _coreCount ?? Environment.ProcessorCount;
        var estimatedLength = EstimateResultLength(_cachedEditorText) + (cores * 50);

        var buffer = ArrayPool<char>.Shared.Rent(estimatedLength);

        try
        {
            var currentPos = 0;

            // Start
            currentPos += ReplaceAllPlaceholders(
                _cachedEditorText.AsSpan(0, _startIndex),
                buffer.AsSpan(currentPos),
                sensorsInformation);

            // Middle - cpu cores
            currentPos += CalculateCoreMetricsToSpan(
                buffer.AsSpan(currentPos),
                sensorsInformation.CpuFrequencyPerCore,
                sensorsInformation.CpuVoltagePerCore);

            // End
            currentPos += ReplaceAllPlaceholders(
                _cachedEditorText.AsSpan(_endIndex + 21),
                buffer.AsSpan(currentPos),
                sensorsInformation);

            RtssHandler.ChangeOsdTextSpan(buffer.AsSpan(0, currentPos));
        }
        finally
        {
            ArrayPool<char>.Shared.Return(buffer);
        }
    }

    private int ReplaceAllPlaceholders(ReadOnlySpan<char> input, Span<char> output,
        SensorsInformation sensorsInformation)
    {
        var current = input;
        var outputPos = 0;

        while (!current.IsEmpty)
        {
            var dollarIndex = current.IndexOf('$');
            if (dollarIndex == -1)
            {
                current.CopyTo(output[outputPos..]);
                outputPos += current.Length;
                break;
            }

            current[..dollarIndex].CopyTo(output[outputPos..]);
            outputPos += dollarIndex;

            var remaining = current[dollarIndex..];
            var endDollarIndex = remaining[1..].IndexOf('$');

            if (endDollarIndex == -1)
            {
                remaining.CopyTo(output[outputPos..]);
                outputPos += remaining.Length;
                break;
            }

            var placeholder = remaining[..(endDollarIndex + 2)];
            var replacementLength = TryReplacePlaceholder(placeholder, output[outputPos..], sensorsInformation);

            if (replacementLength > 0)
            {
                outputPos += replacementLength;
                current = remaining[(endDollarIndex + 2)..];
            }
            else
            {
                placeholder.CopyTo(output[outputPos..]);
                outputPos += placeholder.Length;
                current = remaining[(endDollarIndex + 2)..];
            }
        }

        return outputPos;
    }

    private int TryReplacePlaceholder(ReadOnlySpan<char> placeholder, Span<char> output,
        SensorsInformation sensorsInformation)
    {
        if (placeholder.Length < 3) return 0;

        return placeholder switch
        {
            "$AppVersion$" => WriteToSpan("", output),
            "$SelectedPreset$" => WriteTransliteratedPreset(output),
            "$stapm_value$" => WriteFormattedDouble(sensorsInformation.CpuStapmValue, output),
            "$stapm_limit$" => WriteFormattedDouble(sensorsInformation.CpuStapmLimit, output),
            "$fast_value$" => WriteFormattedDouble(sensorsInformation.CpuFastValue, output),
            "$fast_limit$" => WriteFormattedDouble(sensorsInformation.CpuFastLimit, output),
            "$slow_value$" => WriteFormattedDouble(sensorsInformation.CpuSlowValue, output),
            "$slow_limit$" => WriteFormattedDouble(sensorsInformation.CpuSlowLimit, output),
            "$vrmedc_value$" => WriteFormattedDouble(sensorsInformation.VrmEdcValue, output),
            "$vrmedc_max$" => WriteFormattedDouble(sensorsInformation.VrmEdcLimit, output),
            "$cpu_temp_value$" => WriteFormattedDouble(sensorsInformation.CpuTempValue, output),
            "$cpu_temp_max$" => WriteFormattedDouble(sensorsInformation.CpuTempLimit, output),
            "$cpu_usage$" => WriteFormattedDouble(sensorsInformation.CpuUsage, output),
            "$gfx_clock$" => WriteFormattedDouble(sensorsInformation.ApuFrequency, output),
            "$gfx_volt$" => WriteFormattedDouble(sensorsInformation.ApuVoltage, output),
            "$gfx_temp$" => WriteFormattedDouble(sensorsInformation.ApuTempValue, output),
            "$average_cpu_clock$" => WriteFormattedDouble(sensorsInformation.CpuFrequency, output),
            "$average_cpu_voltage$" => WriteFormattedDouble(sensorsInformation.CpuVoltage, output),
            _ => 0
        };
    }

    private static int WriteToSpan(string text, Span<char> output)
    {
        if (string.IsNullOrEmpty(text)) return 0;

        var span = text.AsSpan();
        span.CopyTo(output);
        return span.Length;
    }

    private static int WriteFormattedDouble(double value, Span<char> output)
    {
        return value.TryFormat(output, out var written, "0.###") ? written : 0;
    }

    private int WriteTransliteratedPreset(Span<char> output)
    {
        if (string.IsNullOrEmpty(_appliedPreset)) return 0;

        var written = 0;
        foreach (var c in _appliedPreset)
        {
            var transliterated = GetTransliteration(c);
            if (!transliterated.IsEmpty)
            {
                if (written + transliterated.Length <= output.Length)
                {
                    transliterated.CopyTo(output[written..]);
                    written += transliterated.Length;
                }
            }
            else if (written < output.Length)
            {
                output[written++] = c;
            }
        }

        return written;
    }

    private int CalculateCoreMetricsToSpan(Span<char> output, double[]? cpuFrequencyPerCore,
        double[]? cpuVoltagePerCore)
    {
        if (string.IsNullOrEmpty(_coreTemplate)) return 0;

        var cores = _coreCount ?? Environment.ProcessorCount;
        var outputPos = 0;

        outputPos += WriteToSpan(_compactSizing, output[outputPos..]);

        for (uint f = 0; f < cores; f++)
        {
            if (f > 0 && f % 4 == 0) outputPos += WriteToSpan(_compactSizing, output[outputPos..]);

            outputPos += ProcessCoreTemplate(_coreTemplate, f, cpuFrequencyPerCore, cpuVoltagePerCore,
                output[outputPos..]);
        }

        return outputPos;
    }

    private int ProcessCoreTemplate(string template, uint coreIndex, double[]? frequencies,
        double[]? voltages, Span<char> output)
    {
        var clk = GetSafeCoreValue(frequencies, coreIndex);
        var volt = GetSafeCoreValue(voltages, coreIndex);

        var templateSpan = template.AsSpan();
        var outputPos = 0;

        while (!templateSpan.IsEmpty)
        {
            var dollarIndex = templateSpan.IndexOf('$');
            if (dollarIndex == -1)
            {
                templateSpan.CopyTo(output[outputPos..]);
                outputPos += templateSpan.Length;
                break;
            }

            templateSpan[..dollarIndex].CopyTo(output[outputPos..]);
            outputPos += dollarIndex;

            var remaining = templateSpan[dollarIndex..];
            if (remaining.StartsWith("$currCore$"))
            {
                outputPos += coreIndex.TryFormat(output[outputPos..], out var written) ? written : 0;
                templateSpan = remaining[10..];
            }
            else if (remaining.StartsWith("$cpu_core_clock$"))
            {
                outputPos += clk.TryFormat(output[outputPos..], out var written, "F3") ? written : 0;
                templateSpan = remaining[16..];
            }
            else if (remaining.StartsWith("$cpu_core_voltage$"))
            {
                outputPos += volt.TryFormat(output[outputPos..], out var written, "G3") ? written : 0;
                templateSpan = remaining[18..];
            }
            else
            {
                output[outputPos++] = '$';
                templateSpan = remaining[1..];
            }
        }

        return outputPos;
    }

    private static int EstimateResultLength(string input)
    {
        return Math.Max(input.Length + input.Length / 2, 1024);
    }

    private static double GetSafeCoreValue(double[]? array, uint index)
    {
        return array != null && index < array.Length ? array[index] : 0f;
    }

    private static ReadOnlySpan<char> GetTransliteration(char c) => c switch
    {
        'а' => "a", 'б' => "b", 'в' => "v", 'г' => "g", 'д' => "d",
        'е' => "e", 'ё' => "yo", 'ж' => "zh", 'з' => "z", 'и' => "i",
        'й' => "y", 'к' => "k", 'л' => "l", 'м' => "m", 'н' => "n",
        'о' => "o", 'п' => "p", 'р' => "r", 'с' => "s", 'т' => "t",
        'у' => "u", 'ф' => "f", 'х' => "h", 'ц' => "ts", 'ч' => "ch",
        'ш' => "sh", 'щ' => "sch", 'ъ' => "'", 'ы' => "i", 'ь' => "'",
        'э' => "e", 'ю' => "yu", 'я' => "ya",

        'А' => "A", 'Б' => "B", 'В' => "V", 'Г' => "G", 'Д' => "D",
        'Е' => "E", 'Ё' => "Yo", 'Ж' => "Zh", 'З' => "Z", 'И' => "I",
        'Й' => "Y", 'К' => "K", 'Л' => "L", 'М' => "M", 'Н' => "N",
        'О' => "O", 'П' => "P", 'Р' => "R", 'С' => "S", 'Т' => "T",
        'У' => "U", 'Ф' => "F", 'Х' => "H", 'Ц' => "Ts", 'Ч' => "Ch",
        'Ш' => "Sh", 'Щ' => "Sch", 'Ъ' => "'", 'Ы' => "I", 'Ь' => "'",
        'Э' => "E", 'Ю' => "Yu", 'Я' => "Ya",
        _ => null
    };

    [GeneratedRegex(@"\$cpu_clock_cycle\$(.*?)\$cpu_clock_cycle_end\$")]
    private static partial Regex ClockCycleRegex();
}