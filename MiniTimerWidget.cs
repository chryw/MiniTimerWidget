using System;
using System.Text.Json;
using System.Threading;

namespace MiniTimerWidget
{
  internal class MiniTimerWidget : WidgetImplBase
  {
    public static string DefinitionId { get; } = "MiniTimer_Widget";
    private static string WidgetTemplate { get; set; } = "";
    private Timer? _timer;
    private int _remainingSeconds = 5 * 60;
    private string _timerState = "stopped"; // "stopped", "running", "paused"

    private static void Log(string message)
    {
      // Route logs through WidgetProvider for visibility
      Console.WriteLine($"[MiniTimerWidget] {message}");
      System.Diagnostics.Debug.WriteLine($"[MiniTimerWidget] {message}");
    }

    public MiniTimerWidget(string widgetId, string initialState) : base(widgetId, initialState)
    {
      Log($"MiniTimerWidget created: id={widgetId}, initialState={initialState}");
      if (!string.IsNullOrEmpty(initialState) && initialState.TrimStart().StartsWith("{"))
      {
        try
        {
            using var doc = JsonDocument.Parse(initialState);
            if (doc.RootElement.TryGetProperty("RemainingSeconds", out _) &&
                doc.RootElement.TryGetProperty("State", out _))
            {
                var stateObj = JsonSerializer.Deserialize<TimerState>(initialState);
                if (stateObj != null)
                {
                    _remainingSeconds = stateObj.RemainingSeconds;
                    _timerState = stateObj.State ?? "stopped";
                }
            }
            else
            {
                Log($"initialState JSON does not contain required properties: {initialState}");
            }
        }
        catch (Exception ex)
        {
          Log($"Exception deserializing initialState: '{initialState}'. Exception: {ex}");
        }
      }
      else if (!string.IsNullOrEmpty(initialState))
      {
        Log($"initialState is not valid JSON: '{initialState}'");
      }
    }

    private static string GetDefaultTemplate()
    {
      if (string.IsNullOrEmpty(WidgetTemplate))
      {
        WidgetTemplate = ReadPackageFileFromUri("ms-appx:///Templates/MiniTimerWidgetTemplate.json");
      }
      return WidgetTemplate;
    }

    public override string GetTemplateForWidget() => GetDefaultTemplate();

    public override string GetDataForWidget()
    {
      try
      {
        string timeStr;
        var ts = TimeSpan.FromSeconds(_remainingSeconds);
        if (_remainingSeconds >= 3600)
        {
          // Format as HH:mm:ss
          timeStr = $"{(int)ts.TotalHours:D2}:{ts.Minutes:D2}:{ts.Seconds:D2}";
        }
        else
        {
          // Format as mm:ss
          timeStr = $"{ts.Minutes:D2}:{ts.Seconds:D2}";
        }
        var data = JsonSerializer.Serialize(new { time = timeStr, state = _timerState });
        Log($"GetDataForWidget: {data}");
        return data;
      }
      catch (Exception ex)
      {
        Log($"GetDataForWidget Exception: {ex.Message}");
        // Fallback to a safe default
        return JsonSerializer.Serialize(new { time = "5:00", state = "stopped" });
      }
    }

    public override void OnActionInvoked(Microsoft.Windows.Widgets.Providers.WidgetActionInvokedArgs args)
    {
      Log($"[MiniTimerWidget] OnActionInvoked override called for id={id}");
      Log($"OnActionInvoked: args.Data={args.Data}");
      // Defensive: args.Data may be null or not valid JSON
      if (!string.IsNullOrEmpty(args.Data))
      {
        try
        {
          using var doc = JsonDocument.Parse(args.Data);
          if (doc.RootElement.TryGetProperty("id", out var idProp))
          {
            var actionId = idProp.GetString();
            string timeValue = null;
            if (doc.RootElement.TryGetProperty("time", out var timeProp))
            {
              timeValue = timeProp.GetString();
              Log($"Button click data: time={timeValue}");
            }
            Log($"Action invoked: {actionId}");
            switch (actionId)
            {
              case "startTimer":
                Log("Start button clicked");
                StartTimer();
                break;
              case "pauseTimer":
                Log("Pause button clicked");
                PauseTimer();
                break;
              case "stopTimer":
                Log("Stop button clicked");
                StopTimer();
                break;
            }
          }
        }
        catch (Exception ex)
        {
          Log($"OnActionInvoked Exception: {ex.Message}");
        }
      }
    }

    private void StartTimer()
    {
      Log("StartTimer called");
      if (_timerState == "running") return;
      _timerState = "running";
      _timer?.Dispose();
      _timer = new Timer(_ => Tick(), null, 1000, 1000);
      UpdateWidget();
    }

    private void PauseTimer()
    {
      Log("PauseTimer called");
      if (_timerState != "running") return;
      _timerState = "paused";
      _timer?.Dispose();
      _timer = null;
      UpdateWidget();
    }

    private void StopTimer()
    {
      Log("StopTimer called");
      _timerState = "stopped";
      _remainingSeconds = 15 * 60;
      _timer?.Dispose();
      _timer = null;
      UpdateWidget();
    }

    private void Tick()
    {
      if (_timerState != "running") return;
      _remainingSeconds--;
      if (_remainingSeconds <= 0)
      {
        _remainingSeconds = 0;
        _timerState = "stopped";
        _timer?.Dispose();
        _timer = null;
      }
      // For debugging: update the widget every second
      UpdateWidget();
    }

    private void UpdateWidget()
    {
      try
      {
        state = JsonSerializer.Serialize(new TimerState { RemainingSeconds = _remainingSeconds, State = _timerState });
        var options = new Microsoft.Windows.Widgets.Providers.WidgetUpdateRequestOptions(id)
        {
          Template = GetTemplateForWidget(),
          Data = GetDataForWidget(),
          CustomState = state
        };
        Microsoft.Windows.Widgets.Providers.WidgetManager.GetDefault().UpdateWidget(options);
        Log($"UpdateWidget: state={state}");
      }
      catch (Exception ex)
      {
        Log($"{ex.Message}");
      }
    }

    private class TimerState
    {
      public int RemainingSeconds { get; set; }
      public string State { get; set; } = "stopped";
    }
  }
}