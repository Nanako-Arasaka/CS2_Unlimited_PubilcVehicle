import { copy } from "./copy.js";

const app = document.querySelector("#app");
const languageKey = "vehicle-cap-unlock-language";
const state = {
  data: null,
  language: localStorage.getItem(languageKey) === "en" ? "en" : "zh"
};

const icons = {
  arrow: '<path d="M5 12h14M13 6l6 6-6 6"/>',
  external: '<path d="M14 5h5v5M10 14l9-9M19 14v5H5V5h5"/>',
  check: '<path d="m5 12 4 4L19 6"/>',
  circle: '<circle cx="12" cy="12" r="7"/>',
  copy: '<rect x="8" y="8" width="11" height="11" rx="2"/><path d="M16 8V5H5v11h3"/>',
  chevron: '<path d="m7 10 5 5 5-5"/>',
  code: '<path d="m8 7-5 5 5 5M16 7l5 5-5 5M14 4l-4 16"/>',
  file: '<path d="M6 3h8l4 4v14H6zM14 3v5h4M9 12h6M9 16h6"/>',
  shield: '<path d="M12 3 5 6v5c0 5 3 8 7 10 4-2 7-5 7-10V6z"/><path d="m9 12 2 2 4-5"/>',
  sliders: '<path d="M4 7h16M4 17h16"/><circle cx="9" cy="7" r="3"/><circle cx="15" cy="17" r="3"/>',
  info: '<circle cx="12" cy="12" r="9"/><path d="M12 11v6M12 7h.01"/>'
};

function icon(name, className = "icon") {
  return `<svg class="${className}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${icons[name] || icons.info}</svg>`;
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#039;");
}

function format(template, values) {
  return Object.entries(values).reduce(
    (result, [key, value]) => result.replaceAll(`{${key}}`, String(value)),
    template
  );
}

function externalLink(url, label, className = "text-link", withIcon = true) {
  const t = copy[state.language];
  return `<a class="${className}" href="${escapeHtml(url)}" target="_blank" rel="noopener noreferrer">${withIcon ? icon("external") : ""}<span>${escapeHtml(label)}</span><span class="sr-only">(${escapeHtml(t.actions.external)})</span></a>`;
}

function renderIllustration(t) {
  return `
    <div class="media-frame">
      <svg class="range-illustration" viewBox="0 0 800 450" role="img" aria-label="${escapeHtml(t.hero.illustrationAlt)}">
        <g class="diagram-grid" aria-hidden="true">
          <path d="M64 96H736M64 225H736M64 354H736"/>
          <path d="M128 54V382M272 54V382M416 54V382M560 54V382M704 54V382"/>
        </g>
        <g class="diagram-heading">
          <text x="64" y="76" class="diagram-label">${escapeHtml(t.hero.original)}</text>
          <text x="64" y="105" class="diagram-value">${escapeHtml(t.hero.originalValue)}</text>
          <text x="64" y="251" class="diagram-label">${escapeHtml(t.hero.extended)}</text>
          <text x="64" y="280" class="diagram-value diagram-value--accent">${escapeHtml(t.hero.extendedValue)}</text>
        </g>
        <g class="diagram-track" aria-hidden="true">
          <path d="M64 157H736" class="track-base"/>
          <path d="M64 157H560" class="track-active"/>
          <path d="M64 332H736" class="track-base"/>
          <path d="M64 332H560" class="track-active"/>
          <path d="M560 332H704" class="track-extension"/>
          <path d="M64 145V169M560 145V169M736 145V169M64 320V344M560 320V344M704 320V344" class="track-ticks"/>
          <circle cx="560" cy="157" r="13" class="track-knob"/>
          <circle cx="704" cy="332" r="13" class="track-knob track-knob--extended"/>
        </g>
        <g class="diagram-endpoints" aria-hidden="true">
          <text x="560" y="194" text-anchor="middle">1×</text>
          <text x="704" y="370" text-anchor="middle">5×</text>
          <text x="64" y="411">${escapeHtml(t.hero.min)}</text>
          <text x="704" y="244" text-anchor="end" class="diagram-example">${escapeHtml(t.hero.example)}</text>
        </g>
      </svg>
      <span class="media-badge">${escapeHtml(t.hero.illustration)}</span>
    </div>
    <p class="media-note">${escapeHtml(t.hero.mediaNote)}</p>`;
}

function renderSpecs(data, t) {
  const rows = [
    ["status", `<span class="status status--${escapeHtml(data.status)}">${escapeHtml(t.status[data.status])}</span>`],
    ["version", data.version],
    ["game", data.gameVersion],
    ["type", data.type],
    ["platform", data.platform],
    ["author", data.author],
    ["updated", data.updated],
    ["license", data.license]
  ].filter(([, value]) => value !== null && value !== undefined && value !== "");

  return `
    <section class="side-card specs-card" aria-labelledby="specs-title">
      <h2 class="eyebrow" id="specs-title">${escapeHtml(t.specs.title)}</h2>
      <dl class="spec">
        ${rows.map(([key, value]) => `
          <div class="spec__row">
            <dt class="spec__key">${escapeHtml(t.specs[key])}</dt>
            <dd class="spec__val">${key === "status" ? value : escapeHtml(value)}</dd>
          </div>`).join("")}
      </dl>
    </section>`;
}

function renderSettings(data, t) {
  return `
    <div class="settings-table" role="table" aria-label="${escapeHtml(t.sections.settings)}">
      <div class="setting-row setting-row--head" role="row">
        <span role="columnheader" class="setting-name">${escapeHtml(t.sections.settings)}</span>
        <span role="columnheader">${escapeHtml(t.settings.default)}</span>
        <span role="columnheader">${escapeHtml(t.settings.range)}</span>
        <span role="columnheader" class="setting-description">${escapeHtml(t.settings.description)}</span>
      </div>
      ${data.settings.map((setting) => {
        const item = t.settings.items[setting.key];
        const isBoolean = setting.range === "boolean";
        const defaultValue = isBoolean
          ? (setting.default ? t.settings.on : t.settings.off)
          : `${setting.default}×`;
        const range = isBoolean
          ? t.settings.booleanRange
          : format(t.settings.multiplierRange, { min: setting.range[0], max: setting.range[1] });
        return `
          <div class="setting-row" role="row">
            <span role="cell" class="setting-name">${escapeHtml(item.label)}</span>
            <span role="cell" class="setting-value">${escapeHtml(defaultValue)}</span>
            <span role="cell" class="setting-value">${escapeHtml(range)}</span>
            <span role="cell" class="setting-description">${escapeHtml(item.description)}</span>
          </div>`;
      }).join("")}
    </div>
    <p class="section-note">${escapeHtml(t.settings.note)}</p>`;
}

function renderValidation(data, t) {
  return `
    <ul class="validation-list">
      ${data.validation.items.map((item) => {
        const done = item.state === "done";
        return `
          <li class="validation-item validation-item--${escapeHtml(item.state)}">
            <span class="validation-icon">${icon(done ? "check" : "circle")}</span>
            <span class="validation-text">${escapeHtml(t.validation.items[item.id])}</span>
            <span class="validation-state">${escapeHtml(done ? t.validation.done : t.validation.pending)}</span>
          </li>`;
      }).join("")}
    </ul>
    <div class="validation-footer">
      <span>${escapeHtml(format(t.validation.asOf, { date: data.validation.asOf }))}</span>
      ${externalLink(data.distribution.validation, t.actions.validationGuide)}
    </div>
    <p class="section-note">${escapeHtml(t.validation.note)}</p>`;
}

function renderMain(data, t) {
  return `
    <main class="main-content" id="main-content">
      <section class="content-section" id="overview" aria-labelledby="overview-title">
        <h2 id="overview-title">${escapeHtml(t.sections.intro)}</h2>
        <p class="intro-copy">${escapeHtml(t.intro)}</p>
      </section>

      <section class="content-section" id="features" aria-labelledby="features-title">
        <h2 id="features-title">${escapeHtml(t.sections.features)}</h2>
        <div class="feature-list">
          ${t.features.items.map((item, index) => `
            <article class="feature-item">
              <span class="feature-index" aria-hidden="true">0${index + 1}</span>
              <div><h3>${escapeHtml(item.title)}</h3><p>${escapeHtml(item.body)}</p></div>
            </article>`).join("")}
        </div>
        <div class="transport-block">
          <h3 class="transport-title">${escapeHtml(t.features.supportLabel)}</h3>
          <ul class="transport-list">
            ${data.supports.map((item) => `<li>${escapeHtml(t.transport[item])}</li>`).join("")}
          </ul>
        </div>
      </section>

      <section class="content-section" id="settings" aria-labelledby="settings-title">
        <h2 id="settings-title">${escapeHtml(t.sections.settings)}</h2>
        ${renderSettings(data, t)}
      </section>

      <section class="content-section" id="validation" aria-labelledby="validation-title">
        <h2 id="validation-title">${escapeHtml(t.sections.validation)}</h2>
        ${renderValidation(data, t)}
      </section>

      <section class="content-section" id="install" aria-labelledby="install-title">
        <h2 id="install-title">${escapeHtml(t.sections.install)}</h2>
        <div class="source-notice">
          <span class="source-notice__icon">${icon("info")}</span>
          <div>
            <h3>${escapeHtml(data.distribution.paradoxMods ? t.install.paradoxTitle : t.install.sourceOnly)}</h3>
            <p>${escapeHtml(data.distribution.paradoxMods ? t.install.paradoxDetail : t.install.sourceDetail)}</p>
          </div>
        </div>
        <div class="install-links">
          ${data.distribution.paradoxMods
            ? externalLink(data.distribution.paradoxMods, t.actions.subscribe, "btn btn--outline") + externalLink(data.distribution.github, t.actions.repository, "text-link")
            : externalLink(data.distribution.github, t.actions.repository, "btn btn--outline") + externalLink(data.distribution.readme, t.actions.buildGuide, "text-link")}
        </div>
        ${data.distribution.paradoxMods ? "" : `
          <div class="command-block">
            <div class="command-heading">
              <span>${escapeHtml(t.install.commandLabel)}</span>
              <button class="btn btn--icon copy-command" type="button" aria-label="${escapeHtml(t.install.copy)}" title="${escapeHtml(t.install.copy)}">${icon("copy")}</button>
            </div>
            <code>${escapeHtml(t.install.command)}</code>
            <span class="copy-feedback" role="status" aria-live="polite"></span>
          </div>`}
      </section>

      <section class="content-section" id="limitations" aria-labelledby="limitations-title">
        <h2 id="limitations-title">${escapeHtml(t.sections.limits)}</h2>
        <ul class="limitations-list">
          ${t.limits.items.map((item) => `<li>${escapeHtml(item)}</li>`).join("")}
        </ul>
      </section>

      <section class="content-section" id="faq" aria-labelledby="faq-title">
        <h2 id="faq-title">${escapeHtml(t.sections.faq)}</h2>
        <div class="details-list">
          ${t.faq.items.map((item) => `
            <details>
              <summary><span>${escapeHtml(item.question)}</span>${icon("chevron")}</summary>
              <p>${escapeHtml(item.answer)}</p>
            </details>`).join("")}
        </div>
      </section>

      <section class="content-section content-section--developer" aria-labelledby="developer-title">
        <h2 id="developer-title">${escapeHtml(t.sections.developer)}</h2>
        <details class="developer-details">
          <summary><span>${escapeHtml(t.developer.summary)}</span>${icon("chevron")}</summary>
          <div class="developer-body">
            <p>${escapeHtml(t.developer.body)}</p>
            ${externalLink(data.distribution.safety, t.developer.link)}
          </div>
        </details>
      </section>
    </main>`;
}

function renderSideExtras(data, t) {
  return `
    <div class="side-extra">
      <section class="side-card" aria-labelledby="tags-title">
        <h2 class="eyebrow" id="tags-title">${escapeHtml(t.side.tags)}</h2>
        <ul class="tag-list">${t.side.tagItems.map((tag) => `<li class="chip">${escapeHtml(tag)}</li>`).join("")}</ul>
      </section>
      <section class="side-card" aria-labelledby="links-title">
        <h2 class="eyebrow" id="links-title">${escapeHtml(t.side.links)}</h2>
        <nav class="link-list" aria-label="${escapeHtml(t.side.links)}">
          ${externalLink(data.distribution.github, t.actions.repository)}
          ${externalLink(data.distribution.readme, t.actions.buildGuide)}
          ${externalLink(data.distribution.validation, t.actions.validationGuide)}
          ${externalLink(data.distribution.safety, t.actions.safetyGuide)}
        </nav>
      </section>
      <section class="side-card" aria-labelledby="changelog-title">
        <h2 class="eyebrow" id="changelog-title">${escapeHtml(t.side.changelog)}</h2>
        <p class="empty-state">${escapeHtml(t.side.emptyChangelog)}</p>
      </section>
    </div>`;
}

function render() {
  const data = state.data;
  const t = copy[state.language];
  const displayName = data.displayName[state.language];
  const primaryUrl = data.distribution.paradoxMods || data.distribution.github;
  const primaryLabel = data.distribution.paradoxMods ? t.actions.subscribe : t.actions.viewSource;
  const otherLanguage = state.language === "zh" ? "en" : "zh";

  document.documentElement.lang = state.language === "zh" ? "zh-CN" : "en";
  document.title = format(t.meta.title, { name: displayName });
  document.querySelector('meta[name="description"]').content = t.meta.description;

  app.innerHTML = `
    <a class="skip-link" href="#main-content">${escapeHtml(t.a11y.skip)}</a>
    <div class="page" id="top">
      <header class="site-header">
        <div class="header-inner">
          <a class="brand" href="#top" aria-label="${escapeHtml(t.brand.home)}">
            <span class="brand-mark">${icon("sliders")}</span>
            <span class="brand-name">${escapeHtml(t.brand.name)}</span>
            <span class="breadcrumb-separator" aria-hidden="true">›</span>
            <span class="breadcrumb-current">${escapeHtml(data.name)}</span>
          </a>
          <nav class="anchor-nav" aria-label="${escapeHtml(t.nav.label)}">
            <a href="#features">${escapeHtml(t.nav.features)}</a>
            <a href="#settings">${escapeHtml(t.nav.settings)}</a>
            <a href="#validation">${escapeHtml(t.nav.validation)}</a>
            <a href="#install">${escapeHtml(t.nav.install)}</a>
          </nav>
          <div class="language-switch" role="group" aria-label="${escapeHtml(t.language.label)}">
            ${["zh", "en"].map((lang) => `<button type="button" data-language="${lang}" aria-pressed="${state.language === lang}">${escapeHtml(t.language[lang])}</button>`).join("")}
          </div>
        </div>
      </header>

      <div class="page-content">
        <div class="detail">
          <div class="detail__main">
            <section class="media" aria-label="${escapeHtml(t.hero.illustration)}">
              ${renderIllustration(t)}
            </section>
            ${renderMain(data, t)}
          </div>

          <aside class="detail__side">
            <div class="identity">
              <h1>${escapeHtml(displayName)}</h1>
              <p class="author-line">${escapeHtml(t.hero.by)} <span>${escapeHtml(data.author)}</span></p>
              <div class="status-line">
                <span class="status status--${escapeHtml(data.status)}">${escapeHtml(t.status[data.status])}</span>
                <span>${escapeHtml(t.status[`${data.status}Note`])}</span>
              </div>
            </div>
            <div class="primary-action">
              ${externalLink(primaryUrl, primaryLabel, "btn btn--primary", false)}
              <span class="primary-action__icon">${icon("arrow")}</span>
            </div>
            ${renderSpecs(data, t)}
            ${renderSideExtras(data, t)}
          </aside>
        </div>
      </div>

      <footer class="site-footer">
        <div class="footer-inner">
          <p>${escapeHtml(t.footer.disclaimer)}</p>
          <div class="footer-links">
            ${externalLink(data.distribution.github, t.footer.source, "text-link", false)}
            <span>${escapeHtml(t.footer.author)} · ${escapeHtml(data.author)}</span>
            <button class="text-button" type="button" data-language="${otherLanguage}">${escapeHtml(copy[otherLanguage].language[otherLanguage])}</button>
            <a class="text-link" href="#top">${escapeHtml(t.footer.top)}</a>
          </div>
        </div>
      </footer>
    </div>`;
}

function renderError() {
  const t = copy[state.language];
  document.documentElement.lang = state.language === "zh" ? "zh-CN" : "en";
  app.innerHTML = `
    <main class="error-page">
      <section class="error-card">
        ${icon("info", "error-icon")}
        <h1>${escapeHtml(t.error.title)}</h1>
        <p>${escapeHtml(t.error.body)}</p>
        <button class="btn btn--outline" type="button" data-reload>${escapeHtml(t.error.retry)}</button>
      </section>
    </main>`;
}

async function copyCommand(button) {
  const t = copy[state.language];
  const command = t.install.command;
  const feedback = button.closest(".command-block").querySelector(".copy-feedback");
  try {
    await navigator.clipboard.writeText(command);
    feedback.textContent = t.install.copied;
  } catch {
    const textarea = document.createElement("textarea");
    textarea.value = command;
    textarea.className = "clipboard-fallback";
    document.body.append(textarea);
    textarea.select();
    const copied = document.execCommand("copy");
    textarea.remove();
    feedback.textContent = copied ? t.install.copied : t.install.copyFailed;
  }
}

app.addEventListener("click", (event) => {
  const languageButton = event.target.closest("[data-language]");
  if (languageButton) {
    state.language = languageButton.dataset.language;
    localStorage.setItem(languageKey, state.language);
    render();
    return;
  }

  const copyButton = event.target.closest(".copy-command");
  if (copyButton) copyCommand(copyButton);

  if (event.target.closest("[data-reload]")) window.location.reload();
});

async function init() {
  try {
    const response = await fetch("./mod.json");
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    state.data = await response.json();
    render();
  } catch (error) {
    console.error(error);
    renderError();
  }
}

init();
