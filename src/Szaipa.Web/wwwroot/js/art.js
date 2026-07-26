/* 艺术家主页交互。 */
(function () {
    'use strict';

    var bannerPayload = window.__artistBanners || {};

    function normalizeBannerSource(source) {
        if (!source || typeof source.originalUrl !== 'string' || source.originalUrl.length === 0) {
            return null;
        }

        return {
            originalUrl: source.originalUrl,
            avifUrl: typeof source.avifUrl === 'string' && source.avifUrl.length > 0
                ? source.avifUrl
                : null,
        };
    }

    function getBannerBackground(source) {
        if (!source) {
            return 'none';
        }

        var originalBackground = 'url(' + JSON.stringify(source.originalUrl) + ')';
        if (!source.avifUrl) {
            return originalBackground;
        }

        var imageSetBackground = 'image-set(url(' + JSON.stringify(source.avifUrl)
            + ') type("image/avif"), url(' + JSON.stringify(source.originalUrl) + '))';
        return window.CSS && window.CSS.supports('background-image', imageSetBackground)
            ? imageSetBackground
            : originalBackground;
    }

    function onReady(callback) {
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', callback, { once: true });
            return;
        }

        callback();
    }

    function getCollapsibleContent(collapsible) {
        var content = collapsible.nextElementSibling;
        return content && content.classList.contains('content') ? content : null;
    }

    function setCollapsibleState(collapsible, content, isCollapsed) {
        collapsible.classList.toggle('defaultActive', !isCollapsed);
        collapsible.setAttribute('data-collapsed', isCollapsed ? 'true' : 'false');
        if (content) {
            content.style.maxHeight = isCollapsed ? '0px' : content.scrollHeight + 'px';
        }
    }

    var primaryBanner = normalizeBannerSource(bannerPayload.primary);
    var path1Banner = normalizeBannerSource(bannerPayload.path1) || primaryBanner;
    var path2Banner = normalizeBannerSource(bannerPayload.path2) || primaryBanner;

    onReady(function () {
        // 放大镜：由共享 loader 在首次真实交互时加载并初始化。
        window.SzaipaMagnifyLoader?.bind(document);

        // 导航栏的焦点切换
        var sections = Array.from(document.querySelectorAll('#section01, #section02, #section03, #section04'));
        var navigationCells = document.querySelectorAll('#ArtistNav td');

        function updateActiveNavigation() {
            var scrollDistance = window.scrollY;

            sections.forEach(function (section, index) {
                if (section.offsetTop <= scrollDistance) {
                    document.querySelectorAll('#ArtistNav .activeLink').forEach(function (activeCell) {
                        activeCell.classList.remove('activeLink');
                    });

                    var targetCell = navigationCells[index + 1];
                    if (targetCell) {
                        targetCell.classList.add('activeLink');
                    }
                }
            });
        }

        window.addEventListener('scroll', updateActiveNavigation, { passive: true });
        updateActiveNavigation();

        // 背景图的悬浮更换
        var heroSection = document.querySelector('#section01');
        var hoverLines = Array.from(document.querySelectorAll('.hoverLine'));

        if (heroSection) {
            hoverLines.forEach(function (hoverLine, index) {
                hoverLine.addEventListener('mouseenter', function () {
                    var banner = index % 2 === 0 ? path1Banner : path2Banner;
                    heroSection.style.backgroundImage = getBannerBackground(banner);
                });

                hoverLine.addEventListener('mouseleave', function () {
                    heroSection.style.backgroundImage = getBannerBackground(primaryBanner);
                });
            });
        }

        // 年表 - 折叠手风琴
        // 23.12.14：完成更改。
        var collapsibles = Array.from(document.querySelectorAll('.collapsible'));

        // 初始化：默认展开前五个.collapsible
        collapsibles.forEach(function (collapsible, index) {
            var content = getCollapsibleContent(collapsible);

            if (index < 5) {
                setCollapsibleState(collapsible, content, false);
            } else {
                collapsible.setAttribute('data-collapsed', 'true');
            }

            collapsible.addEventListener('click', function () {
                var isCollapsed = collapsible.getAttribute('data-collapsed') === 'true';
                setCollapsibleState(collapsible, content, !isCollapsed);
            });
        });

        // toggleBtn点击事件处理
        var toggleState = true;
        var toggleButton = document.querySelector('#toggleBtn');

        if (toggleButton) {
            toggleButton.addEventListener('click', function () {
                collapsibles.forEach(function (collapsible) {
                    var content = getCollapsibleContent(collapsible);
                    setCollapsibleState(collapsible, content, !toggleState);
                });

                toggleState = !toggleState;
            });
        }

        // Swiper
        var deviceWidth = document.documentElement.clientWidth;
        var deviceHeight = document.documentElement.clientHeight;
        var deviceRatio = deviceWidth / deviceHeight;
        var slidesPerView = deviceRatio > 1 ? 2 : 1;

        new window.Swiper('.mySwiper', {
            spaceBetween: 0,
            slidesPerView: slidesPerView,
            navigation: {
                nextEl: '.swiper-button-next',
                prevEl: '.swiper-button-prev',
            },
            keyboard: {
                enabled: true,
                onlyInViewport: true,
            },
        });

        new window.Swiper('.mySwiper3', {
            spaceBetween: 0,
            pagination: {
                el: '.swiper-pagination',
                clickable: true,
            },
            autoplay: {
                delay: 2500,
                disableOnInteraction: false,
            },
            loop: true,
        });
    });
}());
