// Dual-swiper exhibition gallery (main image swiper + thumbnail swiper), shared by every
// exhibition/publication page. Swiper has no jQuery dependency, so keep this initializer native.
(function () {
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

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', initializeGallery, { once: true });
    } else {
        initializeGallery();
    }
})();
