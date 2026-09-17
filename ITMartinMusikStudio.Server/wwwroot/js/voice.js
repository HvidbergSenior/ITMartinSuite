// Stemmetest: listens to the microphone, finds the sung pitch (autocorrelation
// on the raw waveform - good enough for a single voice), and reports the
// current note plus the lowest/highest note held so far back to Blazor.
window.voiceTest = (() => {
  let ctx, stream, analyser, buf, raf, dotnet;
  let low = null, high = null;   // MIDI numbers
  let lastMidi = null, held = 0;
  const NAMES = ["C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B"];
  const midiName = m => NAMES[m % 12] + (Math.floor(m / 12) - 1);

  function pitchHz(data, sr) {
    // Ignore silence. Low notes are quiet, so the gate is deliberately low.
    let rms = 0; for (let i = 0; i < data.length; i++) rms += data[i] * data[i];
    rms = Math.sqrt(rms / data.length);
    if (rms < 0.004) return -1;
    // Trim to the loud part, then autocorrelate.
    let r1 = 0, r2 = data.length - 1, thr = 0.2;
    for (let i = 0; i < data.length / 2; i++) if (Math.abs(data[i]) < thr) { r1 = i; break; }
    for (let i = 1; i < data.length / 2; i++) if (Math.abs(data[data.length - i]) < thr) { r2 = data.length - i; break; }
    const d = data.slice(r1, r2), n = d.length;
    const c = new Array(n).fill(0);
    for (let i = 0; i < n; i++) for (let j = 0; j < n - i; j++) c[i] += d[j] * d[j + i];
    let k = 0; while (k < n - 1 && c[k] > c[k + 1]) k++;
    let maxv = -1, maxp = -1;
    for (let i = k; i < n; i++) if (c[i] > maxv) { maxv = c[i]; maxp = i; }
    if (maxp <= 0) return -1;
    // Parabolic interpolation for a finer period.
    const x1 = c[maxp - 1] || 0, x2 = c[maxp], x3 = c[maxp + 1] || 0;
    const a = (x1 + x3 - 2 * x2) / 2, b = (x3 - x1) / 2;
    const T = a ? maxp - b / (2 * a) : maxp;
    return sr / T;
  }

  // A note only counts toward the range once it has been held steady for a
  // few frames - a glitchy octave jump or a scrape never becomes "your range".
  function tick() {
    analyser.getFloatTimeDomainData(buf);
    const hz = pitchHz(buf, ctx.sampleRate);
    if (hz > 60 && hz < 1100) {            // human singing range, roughly B1..C6
      const midi = Math.round(69 + 12 * Math.log2(hz / 440));
      held = (midi === lastMidi) ? held + 1 : 0; lastMidi = midi;
      if (held >= 6) {
        if (low === null || midi < low) low = midi;
        if (high === null || midi > high) high = midi;
      }
      dotnet.invokeMethodAsync("VoiceSample", midiName(midi), Math.round(hz),
        low === null ? "–" : midiName(low), high === null ? "–" : midiName(high), low ?? 0, high ?? 0);
    } else { held = 0; lastMidi = null; }
    raf = requestAnimationFrame(tick);
  }

  return {
    start: async (ref) => {
      dotnet = ref; low = high = null; lastMidi = null; held = 0;
      stream = await navigator.mediaDevices.getUserMedia({ audio: { echoCancellation: false, noiseSuppression: false, autoGainControl: false } });
      ctx = new (window.AudioContext || window.webkitAudioContext)();
      const src = ctx.createMediaStreamSource(stream);
      analyser = ctx.createAnalyser(); analyser.fftSize = 4096;   // longer window = reliable down to ~60 Hz
      src.connect(analyser);
      buf = new Float32Array(analyser.fftSize);
      tick();
    },
    stop: () => {
      if (raf) cancelAnimationFrame(raf);
      if (stream) stream.getTracks().forEach(t => t.stop());
      if (ctx) ctx.close();
      raf = stream = ctx = null;
    },
    reset: () => { low = high = null; }
  };
})();
