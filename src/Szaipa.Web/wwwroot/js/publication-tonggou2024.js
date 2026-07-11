// toTopBtn
        const toTopButton = document.querySelector('#topBtn');
        const section01 = document.querySelector('#section01');

        window.addEventListener('scroll', function () {
            // 获取当前滚动位置
            const scrollPosition = window.scrollY;

            // 判断是否在 section01 区域内
            if (scrollPosition >= section01.offsetTop && scrollPosition < section01.offsetTop + section01.offsetHeight) {
                toTopButton.style.display = 'none';
            } else {
                toTopButton.style.display = 'block';
            }
        }, { passive: true });

        toTopButton.addEventListener('click', function () {
            window.scrollTo({
                top: 0,
                behavior: 'smooth' // 添加这个属性可以平滑地滚动到顶部
            });
        });


        // 导航点击加粗
        $(function () {
            $(".nav-item").click(function () {
                $(".nav-item").removeClass("active");
                $(this).addClass("active");
            });
        });

        // section01的go gray
        // $(function () {
        //     var section01Height = $('#section01').height();
        //     var maxScrollTop = $(document).height() - $(window).height();
        //     $(window).scroll(function () {
        //         var currentScrollTop = $(this).scrollTop();
        //         var scrollPercent = (currentScrollTop / (maxScrollTop - section01Height)) * 130;
        //         var grayPercent = Math.max(0, Math.min(100, scrollPercent));
        //         var alpha = Math.max(0, Math.min(1, scrollPercent / 500));
        //         // var scrollPercent = (currentScrollTop / (maxScrollTop - section01Height)) * 900;
        //         // var alpha = Math.max(0, Math.min(1, scrollPercent / 900));
        //         $('#section01').css('filter', 'grayscale(' + grayPercent + '%) brightness(' + (1 - alpha) + ')');
        //     });
        // });


        // sectino03的swiper，coverflow+rotateEffect
        var swiper = new Swiper(".mySwiper", {
            effect: "coverflow",
            // 设置初始显示的幻灯片索引为0
            initialSlide: 1,
            grabCursor: true,
            centeredSlides: true,
            slidesPerView: "auto",
            loop: true,
            // 启用懒加载
            lazy: true,
            // preloadImages: false, // 禁用预加载
            // updateOnImagesReady: true, // 等待所有图片加载完成后更新swiper
            coverflowEffect: {
                rotate: 20,
                stretch: 0,
                depth: 100,
                modifier: 1,
                slideShadows: true,
            },
            navigation: {
                nextEl: ".swiper-button-next",
                prevEl: ".swiper-button-prev",
            },
            pagination: {
                el: ".swiper-pagination",
                clickable: true,
            },
            keyboard: {
                enabled: true,
                onlyInViewport: true,
            },
            on: {
                //扩大点击范围
                click: function (e) {
                    // 获取被点击的slide的索引
                    var clickedIndex = e.clickedIndex;
                    // 切换到被点击的slide
                    this.slideTo(clickedIndex);
                }
            },
        });

        // section03显示Info

        // 获取所有链接
        var links = document.querySelectorAll('.link');

        // 遍历链接，并为每个链接添加事件监听器
        for (var i = 0; i < links.length; i++) {
            links[i].addEventListener('click', function (event) {
                // 阻止链接的默认行为
                event.preventDefault();

                // 获取目标元素
                var target = document.querySelector(this.getAttribute('href'));

                // 移除所有目标元素的 'show' 类
                var targets = document.querySelectorAll('.target');
                for (var j = 0; j < targets.length; j++) {
                    targets[j].classList.remove('displayInfo');
                }

                // 添加 'show' 类到目标元素
                target.classList.add('displayInfo');

                // 将页面滚动到目标元素的位置
                target.scrollIntoView({
                    behavior: 'smooth'
                });
            });
        }


        // section04的双swiper,02是子，03是父

        var swiper2 = new Swiper(".mySwiper2", {
            loop: true,
            grabCursor: true,
            spaceBetween: 3,
            slidesPerView: 7,
            freeMode: true,
            watchSlidesProgress: true,
        });
        var swiper3 = new Swiper(".mySwiper3", {
            loop: true,
            spaceBetween: 0,
            grabCursor: true,
            // slidesPerView: 0,
            // 启用懒加载
            lazy: true,
            navigation: {
                nextEl: ".swiper-button-next",
                prevEl: ".swiper-button-prev",
            },
            thumbs: {
                swiper: swiper2,
            },
            keyboard: {
                enabled: true,
                onlyInViewport: true,
            },
        });

        window.addEventListener('DOMContentLoaded', () => {
            if (!window.$webfont) return;
            $webfont.load(".JhengLight", "06160355f36d4c6eb1fab55d87e32063", "Alibaba-PuHuiTi-L");
            $webfont.load(".JhengRegular", "b548e9e03fa64c0b81a0702c4ac5c8c5", "Alibaba-PuHuiTi-R");
            $webfont.load(".JhengBold", "4424d76bbef84a768011752cfd843c1d", "Alibaba-PuHuiTi-M");
            $webfont.draw();
        }, { once: true });

        $(function () {
            var lastScrollPosition = $(window).scrollTop();
            var downThreshold = 10;  // 向下滚动触发的阈值
            var upThreshold = 5;     // 向上滚动触发的阈值

            $(window).scroll(function () {
                var currentScrollPosition = $(window).scrollTop();
                var distanceScrolled = Math.abs(currentScrollPosition - lastScrollPosition);

                if (currentScrollPosition > lastScrollPosition && distanceScrolled >= downThreshold) {
                    // 向下滚动，并且滚动距离大于或等于向下阈值
                    $(".bannerCon").css({
                        "transition": "transform 250ms",
                        "transform": "translateY(-100%)"
                    });
                } else if (currentScrollPosition < lastScrollPosition && distanceScrolled >= upThreshold) {
                    // 向上滚动，并且滚动距离大于或等于向上阈值
                    $(".bannerCon").css({
                        "transition": "transform 250ms",
                        "transform": "translateY(0)"
                    });
                }

                lastScrollPosition = currentScrollPosition;  // 更新最后的滚动位置
            });
        });
