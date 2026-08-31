// Atlas Stater Theme & Architecture Interop JavaScript

function sanitizeMermaid(raw) {
    if (!raw) return '';
    var text = raw.trim();

    // Remove markdown code blocks if present
    if (text.startsWith('```mermaid')) text = text.substring(10);
    else if (text.startsWith('```')) text = text.substring(3);
    if (text.endsWith('```')) text = text.substring(0, text.length - 3);
    text = text.trim();

    // Replace connector unicode arrows with standard mermaid connectors
    text = text.replace(/\s*→\s*/g, ' --> ');
    text = text.replace(/\s*←\s*/g, ' <-- ');
    text = text.replace(/\s*↔\s*/g, ' <--> ');

    // Replace literal \n with <br/> for clean multiline node labels
    text = text.replace(/\\n/g, '<br/>');

    // Remove surrounding quotes in pipe link labels: -->|"label"| => -->|label|
    text = text.replace(/-->\s*\|"([^"]+)"\|/g, '-->|$1|');
    text = text.replace(/-->\s*\|\\"([^\\"]+)\\"\|/g, '-->|$1|');

    // Remove trailing semicolons from classDef definitions
    text = text.replace(/;\s*$/gm, '');

    return text;
}

function cleanupMermaidErrors() {
    try {
        var rogueElements = document.querySelectorAll('body > [id^="dmermaid-"], body > svg[id^="mermaid-"], body > .error-icon, body > .mermaid-error');
        rogueElements.forEach(function (el) {
            el.remove();
        });
    } catch (e) { }
}

window.renderMermaid = window.renderMermaidInElement = function (elementId, mermaidCode) {
    cleanupMermaidErrors();

    var element = document.getElementById(elementId);
    if (!element) {
        setTimeout(function () {
            var el = document.getElementById(elementId);
            if (el) window.renderMermaid(elementId, mermaidCode);
        }, 100);
        return;
    }

    if (!mermaidCode || !mermaidCode.trim()) {
        element.innerHTML = '<div style="color: var(--text-muted); padding: 2.5rem; text-align: center;">ℹ️ No C4 architecture diagram provided for this view level.</div>';
        return;
    }

    var sanitizedCode = sanitizeMermaid(mermaidCode);

    function doRender() {
        if (!window.mermaid) {
            element.innerHTML = '<div style="color: var(--accent-gold); padding: 1.5rem; text-align: center;">⏳ Loading Mermaid rendering engine...</div>';
            setTimeout(doRender, 200);
            return;
        }

        var isDark = document.documentElement.getAttribute('data-theme') === 'dark';

        try {
            window.mermaid.initialize({
                startOnLoad: false,
                suppressErrorRendering: true,
                theme: isDark ? 'dark' : 'default',
                securityLevel: 'loose',
                themeVariables: isDark ? {
                    darkMode: true,
                    background: '#151D2C',
                    primaryColor: '#8B5CF6',
                    primaryTextColor: '#F1F5F9',
                    primaryBorderColor: '#A78BFA',
                    lineColor: '#67E8F9',
                    secondaryColor: '#1E293B',
                    tertiaryColor: '#0B0F19'
                } : {
                    darkMode: false,
                    background: '#FFFFFF',
                    primaryColor: '#6C5CE7',
                    primaryTextColor: '#1E293B',
                    primaryBorderColor: '#5A45FF',
                    lineColor: '#00D2D3',
                    secondaryColor: '#F1F4F9',
                    tertiaryColor: '#FFFFFF'
                }
            });

            var uniqueId = 'mermaid-' + Math.random().toString(36).substring(2, 9);
            window.mermaid.render(uniqueId, sanitizedCode).then(function (result) {
                cleanupMermaidErrors();
                element.innerHTML = '<div class="mermaid-svg-container" style="width: 100%; display: flex; justify-content: center; overflow-x: auto; padding: 0.5rem;">' + result.svg + '</div>';

                var svgEl = element.querySelector('svg');
                if (svgEl) {
                    svgEl.style.maxWidth = '100%';
                    svgEl.style.height = 'auto';
                    svgEl.style.display = 'block';
                }
            }).catch(function (err) {
                cleanupMermaidErrors();
                console.warn("[Mermaid] Render notice:", err);
                element.innerHTML = '<div style="background: var(--bg-subtle); border: 1px solid var(--border-card); border-radius: 8px; padding: 1.25rem; width: 100%;">' +
                    '<div style="color: var(--accent-coral); font-weight: 700; margin-bottom: 0.5rem;">⚠️ C4 Diagram Render Notice</div>' +
                    '<div style="color: var(--text-secondary); font-size: 0.84rem; margin-bottom: 0.75rem;">' + (err.message || err) + '</div>' +
                    '<pre style="background: var(--bg-card); color: var(--text-muted); padding: 0.75rem; border-radius: 6px; font-size: 0.76rem; overflow-x: auto; max-height: 200px;">' + sanitizedCode + '</pre>' +
                    '</div>';
            });
        } catch (e) {
            cleanupMermaidErrors();
            console.warn("[Mermaid] Exception:", e);
            element.innerHTML = '<div style="background: var(--bg-subtle); border: 1px solid var(--border-card); border-radius: 8px; padding: 1rem; color: var(--accent-coral);">' + e + '</div>';
        }
    }

    doRender();
};

window.copyToClipboard = function (text) {
    if (navigator.clipboard) {
        navigator.clipboard.writeText(text);
        return true;
    }
    return false;
};

window.exportDiagramSvg = function (elementId, filename) {
    var element = document.getElementById(elementId);
    if (!element) return;
    var svg = element.querySelector('svg');
    if (!svg) return;

    var svgData = new XMLSerializer().serializeToString(svg);
    var blob = new Blob([svgData], { type: 'image/svg+xml;charset=utf-8' });
    var url = URL.createObjectURL(blob);
    var a = document.createElement('a');
    a.href = url;
    a.download = (filename || 'c4-architecture-diagram') + '.svg';
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
    URL.revokeObjectURL(url);
};
