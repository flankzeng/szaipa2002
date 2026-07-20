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
        return window.CSS && CSS.supports('background-image', imageSetBackground)
            ? imageSetBackground
            : originalBackground;
    }

    var primaryBanner = normalizeBannerSource(bannerPayload.primary);
    var path1Banner = normalizeBannerSource(bannerPayload.path1) || primaryBanner;
    var path2Banner = normalizeBannerSource(bannerPayload.path2) || primaryBanner;

    // 放大镜：有预览图时先显示预览，首次真实交互才请求原图。
    $(function () {
        $('.zoom').on('pointermove.magnifyLazy mousemove.magnifyLazy focusin.magnifyLazy touchstart.magnifyLazy', function () {
            var $image = $(this);
            if ($image.data('magnifyInitialized')) {
                return;
            }

            $image.data('magnifyInitialized', true);
            $image.off('.magnifyLazy');
            $image.magnify({
                speed: 200,
                // limitBounds: true,
                magnifiedWidth: 1000,
                magnifiedHeight: 1000,
            });
        });
    });

    // 导航栏的焦点切换
    $(function () {
        $(window).scroll(function () {
            var scrollDistance = $(window).scrollTop();
            $('#section01, #section02, #section03, #section04').each(function (i) {
                if ($(this).position().top <= scrollDistance) {
                    $('#ArtistNav .activeLink').removeClass('activeLink');
                    $('#ArtistNav td').eq(i + 1).addClass('activeLink');
                }
            });
        }).scroll();
    });

    // 背景图的悬浮更换
    $(function () {
        $('.hoverLine').hover(function () {
            var index = $('.hoverLine').index(this); // 获取当前 li 元素的下标
            var banner = index % 2 === 0 ? path1Banner : path2Banner;
            $('#section01').css('background-image', getBannerBackground(banner));
        }, function () {
            $('#section01').css('background-image', getBannerBackground(primaryBanner));
        });
    });

    // 年表 - 折叠手风琴
    // 23.12.14：完成更改。
    $(function () {
        // 初始化：默认展开前五个.collapsible
        $('.collapsible').each(function (index) {
            var content = $(this).next('.content');
            if (index < 5) { // 前五个
                $(this).addClass('defaultActive').attr('data-collapsed', 'false');
                content.css('max-height', content.prop('scrollHeight') + 'px');
            } else {
                $(this).attr('data-collapsed', 'true');
            }
        });

        // .collapsible元素的点击事件处理
        $('.collapsible').click(function () {
            var content = $(this).next('.content');
            var isCollapsed = $(this).attr('data-collapsed') === 'true';

            // 切换折叠状态
            if (isCollapsed) {
                $(this).addClass('defaultActive').attr('data-collapsed', 'false');
                content.css('max-height', content.prop('scrollHeight') + 'px');
            } else {
                $(this).removeClass('defaultActive').attr('data-collapsed', 'true');
                content.css('max-height', 0);
            }
        });

        // toggleBtn点击事件处理
        var toggleState = true;
        $('#toggleBtn').click(function () {
            $('.collapsible').each(function () {
                var content = $(this).next('.content');

                if (toggleState) {
                    // 第一次点击：展开所有.collapsible
                    $(this).addClass('defaultActive').attr('data-collapsed', 'false');
                    content.css('max-height', content.prop('scrollHeight') + 'px');
                } else {
                    // 第二次点击：折叠所有.collapsible
                    $(this).removeClass('defaultActive').attr('data-collapsed', 'true');
                    content.css('max-height', 0);
                }
            });
            toggleState = !toggleState; // 切换状态
        });
    });

    // Swiper
    $(function () {
        var deviceWidth = $(window).width(); // 获取设备宽度
        var deviceHeight = $(window).height(); // 获取设备高度

        var deviceRatio = deviceWidth / deviceHeight; // 计算设备宽高比
        var slidesPerView = deviceRatio > 1 ? 2 : 1; // 判断宽高比并设置预览数量

        var swiper = new Swiper(".mySwiper", {
            spaceBetween: 0,
            slidesPerView: slidesPerView,
            navigation: {
                nextEl: ".swiper-button-next",
                prevEl: ".swiper-button-prev",
            },
            keyboard: {
                enabled: true,
                onlyInViewport: true,
            },
        });

        var swiper2 = new Swiper(".mySwiper2", {
            direction: "vertical",
            spaceBetween: 50,
            pagination: {
                el: ".swiper-pagination",
                clickable: true,
            },
        });

        // Swiper3
        var swiper3 = new Swiper(".mySwiper3", {
            spaceBetween: 0,
            pagination: {
                el: ".swiper-pagination",
                clickable: true,
            },
            autoplay: {
                delay: 2500,
                disableOnInteraction: false,
            },
            loop: true,
        });

    });
