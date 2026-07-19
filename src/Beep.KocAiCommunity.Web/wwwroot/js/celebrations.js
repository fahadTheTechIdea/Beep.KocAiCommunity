// KOC celebration effects: a lightweight, dependency-free confetti burst in KOC brand colours.
// Respects prefers-reduced-motion (no animation for users who ask for less motion).
(function () {
    'use strict';

    const COLORS = ['#1466A5', '#5FA3D4', '#2A7CBE', '#D4A017', '#1F8A8C'];

    function prefersReducedMotion() {
        return window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    }

    function burst() {
        if (prefersReducedMotion()) {
            return;
        }

        const canvas = document.createElement('canvas');
        canvas.style.cssText = 'position:fixed;inset:0;pointer-events:none;z-index:20000;';
        canvas.width = window.innerWidth;
        canvas.height = window.innerHeight;
        document.body.appendChild(canvas);
        const ctx = canvas.getContext('2d');

        const originX = canvas.width / 2;
        const originY = Math.min(canvas.height * 0.28, 220);
        const pieces = [];
        for (let i = 0; i < 140; i++) {
            const angle = (Math.PI * 2 * i) / 140;
            const speed = 4 + (i % 7);
            pieces.push({
                x: originX,
                y: originY,
                vx: Math.cos(angle) * speed * (0.6 + (i % 5) / 5),
                vy: Math.sin(angle) * speed - 3,
                size: 5 + (i % 4),
                color: COLORS[i % COLORS.length],
                rot: 0,
                vrot: (i % 2 === 0 ? 1 : -1) * 0.2
            });
        }

        let frame = 0;
        function tick() {
            frame++;
            ctx.clearRect(0, 0, canvas.width, canvas.height);
            for (const p of pieces) {
                p.vy += 0.14;            // gravity
                p.x += p.vx;
                p.y += p.vy;
                p.rot += p.vrot;
                ctx.save();
                ctx.translate(p.x, p.y);
                ctx.rotate(p.rot);
                ctx.fillStyle = p.color;
                ctx.fillRect(-p.size / 2, -p.size / 2, p.size, p.size * 0.6);
                ctx.restore();
            }
            if (frame < 140) {
                requestAnimationFrame(tick);
            } else {
                canvas.remove();
            }
        }
        requestAnimationFrame(tick);
    }

    window.kocCelebrate = function (kind) {
        try {
            burst();
        } catch (e) {
            // Effects are best-effort — never let a rendering hiccup surface to the user.
            console.warn('kocCelebrate failed', e);
        }
    };
})();
