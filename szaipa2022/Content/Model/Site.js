// 便携功能 - 检查PADDING
// 使用方法：在父级设置好padding属性以后，往内部添加一个.padding
// 即可可视化PADDING属性的值

$(function () {
    $(".padding").each(function () {
        var $contentArea = $(this);
        var $paddingArea = $contentArea.parent();

        var paddingTop = parseInt($paddingArea.css("padding-top"));
        var paddingRight = parseInt($paddingArea.css("padding-right"));
        var paddingBottom = parseInt($paddingArea.css("padding-bottom"));
        var paddingLeft = parseInt($paddingArea.css("padding-left"));

        $contentArea.css({
            width: `calc(100% - ${paddingLeft + paddingRight}px)`,
            height: `calc(100% - ${paddingTop + paddingBottom}px)`
        });
    });
});

// UPDATE:2023-05-05