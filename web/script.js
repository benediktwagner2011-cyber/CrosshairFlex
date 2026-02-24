(() => {
  const version = "1.0.0";
  const size = "9.6 MB";
  const year = new Date().getFullYear();
  const path = window.location.pathname.toLowerCase();
  const page = path.endsWith("download.html") ? "download" : "index";
  const translatableIdsByPage = {
    index: [
      "home-sub","home-download","features-title","f1h","f1p","f2h","f2p","f3h","f3p","f4h","f4p",
      "quick-title","q1h","q1p","q2h","q2p","q3h","q3p","q4h","q4p",
      "shots-title","shot1","shot2","shot3",
      "perf-title","perf1h","perf1p","perf2h","perf2p","perf3h","perf3p","perf4h","perf4p","perf-note",
      "faq-title","faq1q","faq1a","faq2q","faq2a","faq3q","faq3a","faq4q","faq4a","faq5q","faq5a","faq6q","faq6a"
    ],
    download: [
      "dl-title","dl-sub","d1h","d1p","d1btn","d2h","d2p","d3h","d3p","d4h","d4p","smart-title","smart-p","back-home"
    ]
  };
  const defaultTexts = {};

  const translations = {
    index: {
      de: {
        "home-sub": "Leichtes Fadenkreuz-Overlay für Windows mit Profil-Teilen, Keybind-Wechsel, vertikaler/horizontaler Home-Ansicht und passiver Eingabe.",
        "home-download": "Download für Windows",
        "features-title": "Funktionen",
        f1h: "Fadenkreuz-Profile",
        f1p: "Erstelle Profile im Add/Edit-Bereich und speichere erst, wenn alles passt. Löschen mit Bestätigung.",
        f2h: "Schneller Keybind-Wechsel",
        f2p: "Wechsle Profile sofort, während die Eingabe passiv für das Spiel bleibt.",
        f3h: "Share Import/Export",
        f3p: "Kopiere Share-Links und importiere Profil-Codes direkt im Share-Bereich.",
        f4h: "Flexible Home-Ansicht",
        f4p: "Wechsle die Home-Ansicht mit einem Klick zwischen vertikal und horizontal.",
        "quick-title": "Schnellstart",
        q1h: "1. Profil erstellen",
        q1p: "Erstelle ein Profil im Add/Edit-Bereich und speichere erst, wenn dein Setup fertig ist.",
        q2h: "2. Home-Ansicht wählen",
        q2p: "Wechsle jederzeit zwischen vertikaler und horizontaler Profilansicht.",
        q3h: "3. Keybinds setzen",
        q3p: "Weise einem Profil einen oder mehrere Hotkeys zu und übernimm sie sofort.",
        q4h: "4. Share + Einstellungen",
        q4p: "Nutze Share-Import/Export und konfiguriere Verhalten, Theme, Sprache und Autostart.",
        "shots-title": "Screenshots",
        shot1: "Overlay-Beispiel",
        shot2: "Profil-Wechsel",
        shot3: "Einstellungen",
        "perf-title": "Leistung (ca.)",
        perf1h: "CPU-Verbrauch",
        perf1p: "Typische Leerlauf-Last liegt bei etwa 0.1% bis 0.5% auf modernen Systemen. Kurze Spitzen beim Profilwechsel sind moeglich.",
        perf2h: "RAM-Verbrauch",
        perf2p: "Meist etwa 40 bis 90 MB im Hintergrund mit einem aktiven Profil.",
        perf3h: "GPU-Verbrauch",
        perf3p: "Normalerweise nahe 0% bis 1%, da nur einfache Overlay-Elemente gezeichnet werden.",
        perf4h: "Game-Impact",
        perf4p: "In den meisten Faellen ist der FPS-Einfluss kaum merkbar. Deaktiviere unnoetige Overlays fuer minimale Last.",
        "perf-note": "Die Werte sind nur Richtwerte und koennen je nach Hardware, Spielmodus, Aufloesung und Hintergrund-Apps variieren.",
        "faq-title": "FAQ",
        faq1q: "Injiziert es in Spiele?",
        faq1a: "Nein. CrosshairFlex ist als externes Overlay umgesetzt und injiziert nicht in Spielprozesse.",
        faq2q: "Kann ich Profile teilen?",
        faq2a: "Ja. Nutze den Share-Bereich, um Profil-Links zu kopieren und Codes zu importieren.",
        faq3q: "Kann ich den Profil-Ansichtsstil wechseln?",
        faq3a: "Ja. Die Home-Seite unterstützt vertikale und horizontale Profil-Layouts.",
        faq4q: "Öffnet sich das Onboarding automatisch?",
        faq4a: "Ja. Beim ersten Start öffnet sich das Onboarding einmal automatisch. Später kannst du es in den Einstellungen erneut öffnen.",
        faq5q: "Kann das in manchen Spielen bannbar sein?",
        faq5a: "Ja, in manchen Spielen kann es je nach Anti-Cheat-Regeln potenziell bannbar sein. Nutzung auf eigenes Risiko.",
        faq6q: "Wie halte ich das Anti-Cheat-Risiko so gering wie moeglich?",
        faq6a: "Nutze es nur dort, wo Overlays erlaubt sind, vermeide Ranked/Competitive bei unklaren Regeln, halte CrosshairFlex aktuell und schliesse andere unnoetige Overlay-Tools. Kein externes Overlay kann ein Null-Risiko garantieren."
      },
      en: null
    },
    download: {
      de: {
        "dl-title": "CrosshairFlex installieren",
        "dl-sub": "Folge diesen Schritten, um die neueste Version unter Windows sicher herunterzuladen und zu installieren.",
        d1h: "1. Installer herunterladen",
        d1p: "Klicke auf den Button unten, um <code>CrosshairFlex_Setup.exe</code> herunterzuladen.",
        d1btn: "Installer herunterladen",
        d2h: "2. Wenn der Browser warnt",
        d2p: "In Edge/Chrome im Download-Menü bei der Datei auf <strong>Behalten</strong> und dann <strong>Trotzdem behalten</strong> klicken.",
        d3h: "3. Setup starten",
        d3p: "Öffne <code>CrosshairFlex_Setup.exe</code>, bestätige UAC und schließe die Installation ab.",
        d4h: "4. Erster Start",
        d4p: "Beim ersten Start öffnet sich das Onboarding-Tutorial automatisch einmal mit allen Schritten.",
        "smart-title": "SmartScreen-Hinweis",
        "smart-p": "Wenn Windows SmartScreen erscheint, klicke auf <strong>Weitere Informationen</strong> und dann auf <strong>Trotzdem ausführen</strong>, nur wenn du dieser Quelle vertraust.",
        "back-home": "&larr; Zurück zur Startseite"
      },
      en: null
    }
  };

  function setText(id, value) {
    const el = document.getElementById(id);
    if (!el || typeof value !== "string") return;
    if (value.includes("<strong>") || value.includes("<code>") || value.includes("&larr;")) {
      el.innerHTML = value;
      return;
    }
    if ("value" in el && (el.tagName === "INPUT" || el.tagName === "TEXTAREA")) {
      el.value = value;
      return;
    }
    el.textContent = value;
  }

  function applyLanguage(lang) {
    if (lang === "en") {
      Object.keys(defaultTexts).forEach((id) => setText(id, defaultTexts[id]));
    } else {
      const pack = translations[page][lang] || {};
      Object.keys(pack).forEach((id) => setText(id, pack[id]));
    }
    document.documentElement.lang = lang;
    localStorage.setItem("cf_lang", lang);
    document.querySelectorAll(".lang-btn").forEach((btn) => {
      btn.classList.toggle("active", btn.getAttribute("data-lang") === lang);
    });
  }

  (translatableIdsByPage[page] || []).forEach((id) => {
    const el = document.getElementById(id);
    if (el) {
      defaultTexts[id] = el.innerHTML;
    }
  });

  const storedLang = localStorage.getItem("cf_lang");
  const initialLang = storedLang === "de" || storedLang === "en" ? storedLang : "en";
  applyLanguage(initialLang);

  document.querySelectorAll(".lang-btn").forEach((btn) => {
    btn.addEventListener("click", () => {
      const lang = btn.getAttribute("data-lang");
      if (lang === "de" || lang === "en") {
        applyLanguage(lang);
      }
    });
  });

  const versionEl = document.getElementById("version");
  const footVersionEl = document.getElementById("foot-version");
  const sizeEl = document.getElementById("size");
  const yearEl = document.getElementById("year");
  if (versionEl) versionEl.textContent = version;
  if (footVersionEl) footVersionEl.textContent = version;
  if (sizeEl) sizeEl.textContent = size;
  if (yearEl) yearEl.textContent = String(year);

  const observer = new IntersectionObserver((entries) => {
    for (let i = 0; i < entries.length; i++) {
      if (entries[i].isIntersecting) {
        entries[i].target.classList.add("on");
        observer.unobserve(entries[i].target);
      }
    }
  }, { rootMargin: "0px 0px -10% 0px" });

  document.querySelectorAll(".block h2,.block article,.block figure,.faq details").forEach((el) => {
    el.classList.add("reveal");
    observer.observe(el);
  });

  if (page === "index") {
    const heroTitle = document.getElementById("hero-title");
    if (heroTitle) {
      const onScroll = () => {
        const rect = heroTitle.getBoundingClientRect();
        document.body.classList.toggle("compact-brand", rect.top < 24);
      };
      window.addEventListener("scroll", onScroll, { passive: true });
      onScroll();
    }
  }
})();
