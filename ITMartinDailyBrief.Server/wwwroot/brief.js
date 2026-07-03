window.briefPrefs = {
    getInt:    (k)    => { const v = localStorage.getItem(k); return v === null ? null : parseInt(v); },
    getBool:   (k)    => { const v = localStorage.getItem(k); return v === null ? null : v === 'true'; },
    getString: (k)    => localStorage.getItem(k),
    setInt:    (k, v) => localStorage.setItem(k, String(v)),
    setBool:   (k, v) => localStorage.setItem(k, String(v)),
    setString: (k, v) => localStorage.setItem(k, v),
};
