// StudyFlow AI - Interactive 3D Tilt, Reveal & Canvas Confetti
(function () {
    'use strict';

    const prefersReducedMotion = window.matchMedia &&
        window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    const hasFinePointer = window.matchMedia &&
        window.matchMedia('(pointer: fine)').matches;

    function initTiltCards() {
        if (prefersReducedMotion || !hasFinePointer) {
            return;
        }

        const tiltElements = document.querySelectorAll('.sf-tilt');
        tiltElements.forEach(function (el) {
            const maxAngle = parseFloat(el.getAttribute('data-tilt-max') || '6');
            let resetTimer = null;

            el.addEventListener('mouseenter', function () {
                if (resetTimer) {
                    clearTimeout(resetTimer);
                    resetTimer = null;
                }
                el.style.transition = 'transform 0.12s ease-out, box-shadow 0.25s ease';
            });

            el.addEventListener('mousemove', function (e) {
                const rect = el.getBoundingClientRect();
                if (!rect.width || !rect.height) {
                    return;
                }

                const relX = (e.clientX - rect.left) / rect.width;
                const relY = (e.clientY - rect.top) / rect.height;

                const rotateY = ((relX - 0.5) * 2 * maxAngle).toFixed(2);
                const rotateX = (((0.5 - relY) * 2) * maxAngle).toFixed(2);

                el.style.setProperty('--mx', (relX * 100).toFixed(1) + '%');
                el.style.setProperty('--my', (relY * 100).toFixed(1) + '%');
                el.style.transform =
                    `perspective(1000px) rotateX(${rotateX}deg) rotateY(${rotateY}deg) translateY(-4px) scale(1.01)`;
            });

            el.addEventListener('mouseleave', function () {
                el.style.transition = 'transform 0.4s cubic-bezier(0.16, 1, 0.3, 1), box-shadow 0.3s ease';
                el.style.transform = '';
                el.style.removeProperty('--mx');
                el.style.removeProperty('--my');

                resetTimer = setTimeout(function () {
                    el.style.transition = '';
                }, 420);
            });
        });
    }

    function initRevealAnimations() {
        const revealEls = document.querySelectorAll('.sf-reveal');
        if (!revealEls.length) {
            return;
        }

        if (prefersReducedMotion || !('IntersectionObserver' in window)) {
            revealEls.forEach(function (el) {
                el.classList.add('sf-revealed');
            });
            return;
        }

        const observer = new IntersectionObserver(function (entries) {
            entries.forEach(function (entry) {
                if (entry.isIntersecting) {
                    entry.target.classList.add('sf-revealed');
                    observer.unobserve(entry.target);
                }
            });
        }, { threshold: 0.08 });

        revealEls.forEach(function (el) {
            observer.observe(el);
        });
    }

    window.sfLaunchConfetti = function (durationMs) {
        if (prefersReducedMotion) {
            return;
        }

        const duration = typeof durationMs === 'number' && durationMs > 0 ? durationMs : 2600;
        const canvas = document.createElement('canvas');
        canvas.setAttribute('aria-hidden', 'true');
        canvas.style.position = 'fixed';
        canvas.style.inset = '0';
        canvas.style.width = '100%';
        canvas.style.height = '100%';
        canvas.style.pointerEvents = 'none';
        canvas.style.zIndex = '2000';
        document.body.appendChild(canvas);

        const ctx = canvas.getContext('2d');
        if (!ctx) {
            canvas.remove();
            return;
        }

        function resizeCanvas() {
            const dpr = Math.min(window.devicePixelRatio || 1, 2);
            canvas.width = Math.floor(window.innerWidth * dpr);
            canvas.height = Math.floor(window.innerHeight * dpr);
            ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        }

        resizeCanvas();
        window.addEventListener('resize', resizeCanvas);

        const colors = ['#6366f1', '#ec4899', '#06b6d4', '#10b981', '#f59e0b', '#8b5cf6', '#f43f5e'];
        const particles = [];
        const total = 120;

        for (let i = 0; i < total; i++) {
            const fromLeft = i % 2 === 0;
            const originX = fromLeft ? window.innerWidth * 0.18 : window.innerWidth * 0.82;
            const originY = window.innerHeight * 0.72;
            const angle = fromLeft
                ? (-Math.PI / 2) + (Math.random() * 0.85 - 0.15)
                : (-Math.PI / 2) - (Math.random() * 0.85 - 0.15);
            const speed = 7 + Math.random() * 9;

            particles.push({
                x: originX + (Math.random() * 40 - 20),
                y: originY + (Math.random() * 20 - 10),
                vx: Math.cos(angle) * speed,
                vy: Math.sin(angle) * speed,
                w: 7 + Math.random() * 6,
                h: 4 + Math.random() * 5,
                color: colors[i % colors.length],
                rot: Math.random() * Math.PI * 2,
                vRot: (Math.random() - 0.5) * 0.25,
                alpha: 1
            });
        }

        const startTime = performance.now();

        function renderFrame(now) {
            const elapsed = now - startTime;
            ctx.clearRect(0, 0, window.innerWidth, window.innerHeight);

            let alive = false;
            const fadeStart = duration * 0.65;

            for (let i = 0; i < particles.length; i++) {
                const p = particles[i];
                p.vy += 0.26; // gravity
                p.vx *= 0.99;
                p.x += p.vx;
                p.y += p.vy;
                p.rot += p.vRot;

                if (elapsed > fadeStart) {
                    p.alpha = Math.max(0, 1 - (elapsed - fadeStart) / (duration - fadeStart));
                }

                if (p.alpha > 0.02 && p.y < window.innerHeight + 40) {
                    alive = true;
                    ctx.save();
                    ctx.globalAlpha = p.alpha;
                    ctx.translate(p.x, p.y);
                    ctx.rotate(p.rot);
                    ctx.fillStyle = p.color;
                    ctx.fillRect(-p.w / 2, -p.h / 2, p.w, p.h);
                    ctx.restore();
                }
            }

            if (alive && elapsed < duration + 400) {
                requestAnimationFrame(renderFrame);
            } else {
                window.removeEventListener('resize', resizeCanvas);
                canvas.remove();
            }
        }

        requestAnimationFrame(renderFrame);
    };

    document.addEventListener('DOMContentLoaded', function () {
        initTiltCards();
        initRevealAnimations();
    });
})();
