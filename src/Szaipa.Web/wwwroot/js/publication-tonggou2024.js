(function () {
    'use strict';

    function initializeTopButton() {
        var topButton = document.querySelector('#topBtn');
        var firstSection = document.querySelector('#section01');

        if (!topButton || !firstSection) {
            return;
        }

        window.addEventListener('scroll', function () {
            var scrollPosition = window.scrollY;
            var withinFirstSection = scrollPosition >= firstSection.offsetTop
                && scrollPosition < firstSection.offsetTop + firstSection.offsetHeight;

            topButton.style.display = withinFirstSection ? 'none' : 'block';
        }, { passive: true });

        topButton.addEventListener('click', function () {
            window.scrollTo({
                top: 0,
                behavior: 'smooth',
            });
        });
    }

    function initializeNavigation() {
        var navigationItems = Array.from(document.querySelectorAll('.nav-item'));

        navigationItems.forEach(function (item) {
            item.addEventListener('click', function () {
                navigationItems.forEach(function (candidate) {
                    candidate.classList.remove('active');
                });
                item.classList.add('active');
            });
        });
    }

    function initializeGallery() {
        var thumbnailGallery = document.querySelector('.mySwiper2');
        var mainGallery = document.querySelector('.mySwiper3');

        if (!thumbnailGallery || !mainGallery || typeof Swiper !== 'function') {
            return;
        }

        var thumbnailSwiper = new Swiper(thumbnailGallery, {
            loop: true,
            grabCursor: true,
            spaceBetween: 3,
            slidesPerView: 7,
            freeMode: true,
            watchSlidesProgress: true,
        });

        new Swiper(mainGallery, {
            loop: true,
            spaceBetween: 0,
            grabCursor: true,
            lazy: true,
            navigation: {
                nextEl: '.swiper-button-next',
                prevEl: '.swiper-button-prev',
            },
            thumbs: {
                swiper: thumbnailSwiper,
            },
            keyboard: {
                enabled: true,
                onlyInViewport: true,
            },
        });
    }

    function initializeBannerScroll() {
        var banners = Array.from(document.querySelectorAll('.bannerCon'));
        if (banners.length === 0) {
            return;
        }

        var lastScrollPosition = window.scrollY;

        window.addEventListener('scroll', function () {
            var currentScrollPosition = window.scrollY;
            var distanceScrolled = Math.abs(currentScrollPosition - lastScrollPosition);
            var movingDown = currentScrollPosition > lastScrollPosition && distanceScrolled >= 10;
            var movingUp = currentScrollPosition < lastScrollPosition && distanceScrolled >= 5;

            if (movingDown || movingUp) {
                banners.forEach(function (banner) {
                    banner.style.transition = 'transform 250ms';
                    banner.style.transform = movingDown ? 'translateY(-100%)' : 'translateY(0)';
                });
            }

            lastScrollPosition = currentScrollPosition;
        }, { passive: true });
    }

    function initializePage() {
        initializeTopButton();
        initializeNavigation();
        initializeGallery();
        initializeBannerScroll();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initializePage, { once: true });
    } else {
        initializePage();
    }
})();
