(() => {
    'use strict';

    const banner = document.querySelector('.bannerCon');
    if (!banner) {
        return;
    }

    let lastScrollPosition = window.scrollY;
    let ticking = false;

    window.addEventListener('scroll', () => {
        if (ticking) {
            return;
        }

        ticking = true;
        window.requestAnimationFrame(() => {
            const currentScrollPosition = window.scrollY;
            const distanceScrolled = Math.abs(currentScrollPosition - lastScrollPosition);

            if (currentScrollPosition > lastScrollPosition && distanceScrolled >= 10) {
                banner.style.transition = 'transform 250ms';
                banner.style.transform = 'translateY(-100%)';
            } else if (currentScrollPosition < lastScrollPosition && distanceScrolled >= 5) {
                banner.style.transition = 'transform 250ms';
                banner.style.transform = 'translateY(0)';
            }

            lastScrollPosition = currentScrollPosition;
            ticking = false;
        });
    }, { passive: true });
})();
