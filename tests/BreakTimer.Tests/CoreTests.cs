// Copyright 2026 Petre Hoffmann
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BreakTimer.Core.Models;
using BreakTimer.Core.Monitoring;
using BreakTimer.Core.Services;
using BreakTimer.Core.Timer;
using BreakTimer.Core.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace BreakTimer.Tests;

internal sealed class FakeClock : IClock
{
    public TimeSpan Elapsed { get; set; }
    public void Advance(double seconds) => Elapsed += TimeSpan.FromSeconds(seconds);
}

[TestClass]
public class TimerTests
{
    private static (TimerService t, FakeClock c) Create(int seconds = 600)
    {
        var c = new FakeClock();
        var t = new TimerService(c, null, autoTick: false);
        t.SetDuration(TimeSpan.FromSeconds(seconds));
        return (t, c);
    }

    [TestMethod]
    public void StartRunsAndCountsByElapsedTime()
    {
        var (t, c) = Create();
        Assert.IsTrue(t.Start());
        c.Advance(125.4);
        Assert.AreEqual(TimerState.Running, t.State);
        Assert.AreEqual(600 - 125.4, t.Remaining.TotalSeconds, 0.001);
    }

    [TestMethod]
    public void InvalidTransitionsAreRejected()
    {
        var (t, _) = Create();
        Assert.IsFalse(t.Pause());
        Assert.IsFalse(t.Resume());
        Assert.IsFalse(t.Stop());
        t.Start();
        Assert.IsFalse(t.Start(), "Start bei laufendem Timer");
        Assert.IsFalse(t.Resume());
        t.Pause();
        Assert.IsFalse(t.Pause());
        Assert.IsFalse(t.Start());
        Assert.IsTrue(t.Resume());
    }

    [TestMethod]
    public void PauseFreezesAndResumeContinues()
    {
        var (t, c) = Create();
        t.Start(); c.Advance(100);
        t.Pause(); c.Advance(1000);
        Assert.AreEqual(500, t.Remaining.TotalSeconds, 0.001);
        t.Resume(); c.Advance(50);
        Assert.AreEqual(450, t.Remaining.TotalSeconds, 0.001);
    }

    [TestMethod]
    public void StopAndReset()
    {
        var (t, c) = Create();
        t.Start(); c.Advance(10);
        Assert.IsTrue(t.Stop());
        Assert.AreEqual(TimerState.Stopped, t.State);
        Assert.IsFalse(t.Stop(), "bereits gestoppt");
        t.Reset();
        Assert.AreEqual(TimerState.Ready, t.State);
        Assert.AreEqual(600, t.Remaining.TotalSeconds, 0.001);
    }

    [TestMethod]
    public void AdjustExtendsAndShortens()
    {
        var (t, c) = Create();
        t.Start(); c.Advance(60);
        t.Adjust(TimeSpan.FromMinutes(5));
        Assert.AreEqual(840, t.Remaining.TotalSeconds, 0.001);
        t.Adjust(TimeSpan.FromMinutes(-1));
        Assert.AreEqual(780, t.Remaining.TotalSeconds, 0.001);
        c.Advance(10);
        Assert.AreEqual(770, t.Remaining.TotalSeconds, 0.001);
    }

    [TestMethod]
    public void AdjustInReadyChangesDuration()
    {
        var (t, _) = Create();
        t.Adjust(TimeSpan.FromMinutes(5));
        Assert.AreEqual(TimeSpan.FromMinutes(15), t.Duration);
    }

    [TestMethod]
    public void FinishRaisedOnceAndNeverNegative()
    {
        var (t, c) = Create(10);
        int finished = 0;
        t.Finished += (s, e) => finished++;
        t.Start(); c.Advance(5); t.Poll();
        Assert.AreEqual(0, finished);
        c.Advance(100); t.Poll(); t.Poll();
        Assert.AreEqual(1, finished);
        Assert.AreEqual(TimerState.Finished, t.State);
        Assert.AreEqual(TimeSpan.Zero, t.Remaining);
        Assert.IsTrue(t.Start(), "neue Pause nach Ende möglich");
    }

    [TestMethod]
    public void ShorteningBelowZeroFinishes()
    {
        var (t, c) = Create(120);
        t.Start(); c.Advance(30);
        t.Adjust(TimeSpan.FromMinutes(-10));
        Assert.AreEqual(TimerState.Finished, t.State);
    }

    [TestMethod]
    public void InvalidDurationsRejected()
    {
        var (t, _) = Create();
        Assert.IsFalse(t.SetDuration(TimeSpan.Zero));
        Assert.IsFalse(t.SetDuration(TimeSpan.FromHours(25)));
        Assert.IsFalse(t.SetDuration(TimeSpan.FromSeconds(-5)));
        t.Start();
        Assert.IsFalse(t.SetDuration(TimeSpan.FromMinutes(1)), "während Lauf nicht änderbar");
    }

    [TestMethod]
    public void FormattingAndParsing()
    {
        Assert.AreEqual("12:20", TimeFormatter.Format(TimeSpan.FromSeconds(740)));
        Assert.AreEqual("00:01", TimeFormatter.Format(TimeSpan.FromMilliseconds(100)));
        Assert.AreEqual("1:05:00", TimeFormatter.Format(TimeSpan.FromMinutes(65)));
        Assert.AreEqual("00:00", TimeFormatter.Format(TimeSpan.FromSeconds(-3)));
        Assert.IsTrue(TimeFormatter.TryParseMinutesSeconds("10", "30", out var r));
        Assert.AreEqual(630, r.TotalSeconds);
        Assert.IsFalse(TimeFormatter.TryParseMinutesSeconds("abc", "0", out _));
        Assert.IsFalse(TimeFormatter.TryParseMinutesSeconds("0", "0", out _));
        Assert.IsFalse(TimeFormatter.TryParseMinutesSeconds("-1", "0", out _));
        Assert.IsFalse(TimeFormatter.TryParseMinutesSeconds("2000", "0", out _));
    }
}

internal sealed class FakeProvider : IMonitorProvider
{
    public List<MonitorInfo> List = new List<MonitorInfo>();
    public IList<MonitorInfo> GetMonitors() => List.ToList();
    public static MonitorInfo M(string id, int n, int x, int y, int w, int h, bool primary = false, double dpi = 1) =>
        new MonitorInfo { Id = id, Number = n, IsPrimary = primary, Bounds = new PixelRect(x, y, w, h), WorkingArea = new PixelRect(x, y, w, h - 40), DpiScale = dpi };
}

[TestClass]
public class MonitorTests
{
    private static MonitorService Create(FakeProvider p)
    {
        var s = new MonitorService(p);
        s.Refresh();
        return s;
    }

    [TestMethod]
    public void SingleMonitorUsesControlMonitor()
    {
        var p = new FakeProvider(); p.List.Add(FakeProvider.M("A", 1, 0, 0, 1920, 1080, true));
        var s = Create(p);
        var r = s.ResolveDisplay();
        Assert.AreEqual("A", r.Monitor.Id);
        Assert.IsTrue(r.SharesWithControl);
    }

    [TestMethod]
    public void TwoMonitorsPreferOtherThanControl()
    {
        var p = new FakeProvider();
        p.List.Add(FakeProvider.M("A", 1, 0, 0, 2560, 1440, true));
        p.List.Add(FakeProvider.M("B", 2, 2560, 0, 1920, 1080));
        var s = Create(p);
        Assert.AreEqual("A", s.ResolveControl().Id);
        var r = s.ResolveDisplay();
        Assert.AreEqual("B", r.Monitor.Id);
        Assert.IsFalse(r.SharesWithControl);
    }

    [TestMethod]
    public void ThreeMonitorsSwitchTargetAndNegativeCoordinates()
    {
        var p = new FakeProvider();
        p.List.Add(FakeProvider.M("A", 1, 0, 0, 2560, 1440, true));
        p.List.Add(FakeProvider.M("B", 2, -1920, 100, 1920, 1080, false, 1.25));
        p.List.Add(FakeProvider.M("C", 3, 2560, 0, 3840, 2160, false, 1.5));
        var s = Create(p);
        s.SetPreferredDisplay("C");
        Assert.AreEqual(new PixelRect(2560, 0, 3840, 2160), s.ResolveDisplay().Monitor.Bounds);
        s.SetPreferredDisplay("B");
        var b = s.ResolveDisplay().Monitor;
        Assert.AreEqual(-1920, b.Bounds.X);
        Assert.AreEqual(1.25, b.DpiScale);
        Assert.AreEqual("B", s.FromPoint(-100, 500).Id);
        Assert.AreEqual("A", s.FromPoint(10, 10).Id);
    }

    [TestMethod]
    public void RemovingTargetFallsBackAndRaisesChange()
    {
        var p = new FakeProvider();
        p.List.Add(FakeProvider.M("A", 1, 0, 0, 1920, 1080, true));
        p.List.Add(FakeProvider.M("B", 2, 1920, 0, 1920, 1080));
        var s = Create(p);
        s.SetPreferredDisplay("B");
        int changes = 0;
        s.MonitorsChanged += (o, e) => changes++;
        p.List.RemoveAt(1);
        s.RefreshAndNotify();
        Assert.AreEqual(1, changes);
        var r = s.ResolveDisplay();
        Assert.IsTrue(r.IsFallback);
        Assert.AreEqual("A", r.Monitor.Id);
        // Monitor kehrt zurück
        p.List.Add(FakeProvider.M("B", 2, 1920, 0, 1920, 1080));
        s.RefreshAndNotify();
        Assert.AreEqual("B", s.ResolveDisplay().Monitor.Id);
        Assert.IsFalse(s.ResolveDisplay().IsFallback);
    }

    [TestMethod]
    public void ResolutionChangeIsDetected()
    {
        var p = new FakeProvider(); p.List.Add(FakeProvider.M("A", 1, 0, 0, 1920, 1080, true));
        var s = Create(p);
        p.List[0] = FakeProvider.M("A", 1, 0, 0, 1366, 768, true);
        Assert.IsTrue(s.Refresh());
        Assert.IsFalse(s.Refresh());
    }

    [TestMethod]
    public void CenterIsClampedToWorkArea()
    {
        var r = new PixelRect(-1920, 0, 1366, 728).Center(1320, 860);
        Assert.AreEqual(1320, r.Width);
        Assert.AreEqual(728, r.Height);
        Assert.AreEqual(-1920 + (1366 - 1320) / 2, r.X);
    }
}

[TestClass]
public class StorageTests
{
    private string _dir;
    private AppPaths _paths;

    [TestInitialize]
    public void Init()
    {
        _dir = Path.Combine(Path.GetTempPath(), "bt-tests-" + Guid.NewGuid().ToString("N"));
        _paths = new AppPaths(_dir);
    }

    [TestCleanup]
    public void Cleanup()
    {
        try { Directory.Delete(_dir, true); } catch { }
    }

    [TestMethod]
    public void SettingsRoundTrip()
    {
        var svc = new SettingsService(_paths);
        var s = SettingsService.CreateDefaults();
        s.DurationSeconds = 1234; s.SoundEnabled = true; s.DisplayMonitorId = "X";
        s.CurrentDesign.AccentColor = "#112233"; s.CurrentDesign.Heading = "Hallo \"Welt\"";
        Assert.IsTrue(svc.TrySave(s, out _));
        var l = svc.Load();
        Assert.AreEqual(1234, l.DurationSeconds);
        Assert.IsTrue(l.SoundEnabled);
        Assert.AreEqual("X", l.DisplayMonitorId);
        Assert.AreEqual("#112233", l.CurrentDesign.AccentColor);
        Assert.AreEqual("Hallo \"Welt\"", l.CurrentDesign.Heading);
        Assert.IsNull(svc.LastLoadWarning);
    }

    [TestMethod]
    public void CorruptSettingsFallBackToBackupOrDefaults()
    {
        var svc = new SettingsService(_paths);
        var s = SettingsService.CreateDefaults(); s.DurationSeconds = 777;
        svc.TrySave(s, out _);
        s.DurationSeconds = 888; svc.TrySave(s, out _); // erzeugt .bak mit 777
        File.WriteAllText(_paths.SettingsFile, "{ kaputt ,,,");
        var l = svc.Load();
        Assert.AreEqual(777, l.DurationSeconds);
        Assert.IsNotNull(svc.LastLoadWarning);

        File.WriteAllText(_paths.SettingsFile, "garbage");
        File.Delete(_paths.SettingsBackupFile);
        l = svc.Load();
        Assert.AreEqual(900, l.DurationSeconds);
        Assert.IsNotNull(svc.LastLoadWarning);
    }

    [TestMethod]
    public void InvalidValuesAreNormalized()
    {
        var svc = new SettingsService(_paths);
        _paths.EnsureDirectories();
        File.WriteAllText(_paths.SettingsFile, "{\"DurationSeconds\": -5, \"SoundVolume\": 7, \"CurrentDesign\": {\"AccentColor\": \"rot\", \"CountdownSize\": 99999, \"TextAlignment\": \"Mitte\"}}");
        var l = svc.Load();
        Assert.AreEqual(900, l.DurationSeconds);
        Assert.AreEqual(1.0, l.SoundVolume);
        Assert.AreEqual("#18E6FF", l.CurrentDesign.AccentColor);
        Assert.AreEqual(700, l.CurrentDesign.CountdownSize);
        Assert.AreEqual("Center", l.CurrentDesign.TextAlignment);
    }

    private ThemeService Themes() => new ThemeService(_paths, new ResourceStore(_paths));

    [TestMethod]
    public void BuiltInThemesAreProtected()
    {
        var ts = Themes();
        Assert.AreEqual(5, ts.Themes.Count(t => t.IsBuiltIn));
        var b = ts.Find(BuiltInThemes.NeonCityId);
        Assert.ThrowsException<InvalidOperationException>(() => ts.Save(b));
        Assert.ThrowsException<InvalidOperationException>(() => ts.Delete(b));
        Assert.ThrowsException<InvalidOperationException>(() => ts.Rename(b, "x"));
    }

    [TestMethod]
    public void CustomThemeLifecycle()
    {
        var ts = Themes();
        var copy = ts.Duplicate(ts.Find(BuiltInThemes.MinimalDarkId));
        Assert.IsFalse(copy.IsBuiltIn);
        ts.Rename(copy, "Mein Theme");
        var ts2 = Themes(); ts2.LoadCustomThemes();
        Assert.IsNotNull(ts2.Themes.FirstOrDefault(t => t.Name == "Mein Theme"));
        ts2.Delete(ts2.Themes.First(t => t.Name == "Mein Theme"));
        var ts3 = Themes(); ts3.LoadCustomThemes();
        Assert.AreEqual(5, ts3.Themes.Count);
    }

    [TestMethod]
    public void ThemeExportImportWithImage()
    {
        var ts = Themes();
        string img = Path.Combine(_dir, "bg.png");
        Directory.CreateDirectory(_dir);
        File.WriteAllBytes(img, new byte[] { 1, 2, 3 });
        var t = ts.SaveAs(ts.Find(BuiltInThemes.NeonFutureId), "Export Test");
        t.BackgroundImagePath = img;
        string pkg = Path.Combine(_dir, "t.btheme");
        ts.Export(t, pkg);
        var imported = ts.Import(pkg);
        Assert.AreNotEqual(t.Id, imported.Id);
        Assert.AreNotEqual(t.Name, imported.Name);
        Assert.IsTrue(File.Exists(imported.BackgroundImagePath));
        Assert.IsTrue(imported.BackgroundImagePath.StartsWith(_paths.ResourcesDirectory));
    }

    [TestMethod]
    public void ImportInvalidThemeThrows()
    {
        var ts = Themes();
        string bad = Path.Combine(_dir, "bad.btheme");
        Directory.CreateDirectory(_dir);
        File.WriteAllText(bad, "kein zip");
        Assert.ThrowsException<InvalidDataException>(() => ts.Import(bad));
        string badJson = Path.Combine(_dir, "bad.json");
        File.WriteAllText(badJson, "{{{");
        Assert.ThrowsException<InvalidDataException>(() => ts.Import(badJson));
    }

    [TestMethod]
    public void CorruptCustomThemeFileIsSkipped()
    {
        var ts = Themes();
        _paths.EnsureDirectories();
        File.WriteAllText(Path.Combine(_paths.ThemesDirectory, "x.json"), "###");
        ts.LoadCustomThemes();
        Assert.AreEqual(5, ts.Themes.Count);
        Assert.AreEqual(1, ts.LoadErrors.Count);
    }

    private const string LottieJson = "{\"v\":\"5.7.0\",\"fr\":30,\"ip\":0,\"op\":60,\"w\":100,\"h\":100,\"layers\":[]}";

    [TestMethod]
    public void LottieImportValidatesAndCopies()
    {
        var rs = new ResourceStore(_paths);
        Directory.CreateDirectory(_dir);
        string good = Path.Combine(_dir, "a.json"); File.WriteAllText(good, LottieJson);
        string path = rs.ImportLottie(good);
        Assert.IsTrue(rs.IsManaged(path));
        Assert.IsTrue(File.Exists(path));
        string notLottie = Path.Combine(_dir, "b.json"); File.WriteAllText(notLottie, "{\"x\":1}");
        Assert.ThrowsException<InvalidDataException>(() => rs.ImportLottie(notLottie));
        string wrongExt = Path.Combine(_dir, "c.txt"); File.WriteAllText(wrongExt, LottieJson);
        Assert.ThrowsException<InvalidDataException>(() => rs.ImportLottie(wrongExt));
        Assert.ThrowsException<FileNotFoundException>(() => rs.ImportLottie(Path.Combine(_dir, "missing.json")));
    }

    [TestMethod]
    public void ThemeExportImportKeepsLottie()
    {
        var ts = Themes();
        var rs = new ResourceStore(_paths);
        Directory.CreateDirectory(_dir);
        string src = Path.Combine(_dir, "l.json"); File.WriteAllText(src, LottieJson);
        var t = ts.SaveAs(ts.Find(BuiltInThemes.NeonFutureId), "Lottie Test");
        t.LottiePath = rs.ImportLottie(src);
        t.LottieSpeed = 2.5; t.LottieLayer = "Front"; t.LottieLoop = false;
        string pkg = Path.Combine(_dir, "l.btheme");
        ts.Export(t, pkg);
        var imported = ts.Import(pkg);
        Assert.IsTrue(File.Exists(imported.LottiePath));
        Assert.IsTrue(imported.LottiePath.StartsWith(_paths.ResourcesDirectory));
        Assert.AreEqual(2.5, imported.LottieSpeed);
        Assert.AreEqual("Front", imported.LottieLayer);
        Assert.IsFalse(imported.LottieLoop);
    }

    [TestMethod]
    public void ThemeValidatorClampsLottie()
    {
        var t = new Theme { LottieScale = 99, LottieSpeed = -3, LottieOpacity = 5, LottieLayer = "x", LottieFit = "y", LottiePath = null };
        ThemeValidator.Sanitize(t);
        Assert.AreEqual(3, t.LottieScale);
        Assert.AreEqual(0.1, t.LottieSpeed);
        Assert.AreEqual(1, t.LottieOpacity);
        Assert.AreEqual("Behind", t.LottieLayer);
        Assert.AreEqual("Uniform", t.LottieFit);
        Assert.AreEqual("", t.LottiePath);
    }

    [TestMethod]
    public void ImageImportValidatesAndCopies()
    {
        var rs = new ResourceStore(_paths);
        Directory.CreateDirectory(_dir);
        string good = Path.Combine(_dir, "a.PNG"); File.WriteAllBytes(good, new byte[] { 1 });
        string path = rs.ImportImage(good);
        Assert.IsTrue(rs.IsManaged(path));
        string bad = Path.Combine(_dir, "a.exe"); File.WriteAllBytes(bad, new byte[] { 1 });
        Assert.ThrowsException<InvalidDataException>(() => rs.ImportImage(bad));
        Assert.ThrowsException<FileNotFoundException>(() => rs.ImportImage(Path.Combine(_dir, "missing.png")));
    }

    [TestMethod]
    public void DesignStateTracksDirtyAndProtectsBuiltIn()
    {
        var ts = Themes();
        var src = ts.Find(BuiltInThemes.NeonFutureId);
        var d = new DesignState(src.Clone(), src);
        Assert.IsFalse(d.IsDirty);
        d.Working.Heading = "Geändert";
        Assert.IsTrue(d.IsDirty);
        Assert.IsFalse(d.CanOverwriteSource);
        Assert.AreEqual("Kurze Pause", src.Heading, "Standard-Theme bleibt unverändert");
        var created = d.SaveAs(ts, "Eigenes");
        Assert.IsFalse(d.IsDirty);
        Assert.IsTrue(d.CanOverwriteSource);
        d.Working.Heading = "Nochmal";
        d.SaveToSource(ts);
        Assert.AreEqual("Nochmal", created.Heading);
        d.Working.Heading = "Weg";
        d.Revert();
        Assert.AreEqual("Nochmal", d.Working.Heading);
    }

    [TestMethod]
    public void WaveSynthProducesValidWav()
    {
        foreach (string p in WaveSynth.Presets)
        {
            byte[] w = WaveSynth.Generate(p, 0.5);
            Assert.AreEqual("RIFF", System.Text.Encoding.ASCII.GetString(w, 0, 4));
            Assert.IsTrue(w.Length > 20000);
        }
    }
}
