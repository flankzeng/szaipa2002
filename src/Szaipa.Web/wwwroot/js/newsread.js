/* 新闻详情返回顶部交互。 */
(() => {
    'use strict';

    const toTopButton = document.querySelector('#topBtn');
    if (!toTopButton) {
        return;
    }

    window.addEventListener('scroll', () => {
        toTopButton.style.display = window.scrollY >= 500 ? 'block' : 'none';
    }, { passive: true });

    toTopButton.addEventListener('click', () => {
        window.scrollTo({
            top: 0,
            behavior: 'smooth'
        });
    });
})();
