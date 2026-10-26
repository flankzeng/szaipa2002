// 会员风采排序页（/Staff/VipOrder）：HTML5 拖拽排序 + ▲▼ 按钮，拖完即 POST 保存整份顺序，「保存顺序」按钮兜底。
// 写法与 artist-site.js 的作品排序一致（防伪令牌走 RequestVerificationToken 请求头）。
(function () {
  'use strict';

  function init() {
    var list = document.getElementById('vip-order-list');
    if (!list || !list.getAttribute('data-save-url')) return;

    var dragging = null;
    var startOrder = null;
    var lastSavedOrder = null;
    var inFlightOrder = null;
    var status = document.querySelector('[data-vip-order-status]');
    var saveButton = document.querySelector('[data-save-vip-order]');

    function items() {
      return list.querySelectorAll('[data-artist-id]');
    }

    function getOrder() {
      return Array.prototype.map.call(items(), function (item) {
        return Number(item.getAttribute('data-artist-id'));
      });
    }

    function orderKey(ids) {
      return ids.join(',');
    }

    function renumber() {
      Array.prototype.forEach.call(items(), function (item, index) {
        var position = item.querySelector('[data-vip-position]');
        if (position) position.textContent = String(index + 1);
      });
    }

    function saveOrder(force) {
      var ids = getOrder();
      var key = orderKey(ids);
      if (!force && (key === lastSavedOrder || key === inFlightOrder)) return;

      var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
      var headers = { 'Content-Type': 'application/json' };
      if (tokenInput) headers.RequestVerificationToken = tokenInput.value;
      inFlightOrder = key;
      if (status) status.textContent = '排序保存中…';
      if (saveButton) saveButton.disabled = true;

      fetch(list.getAttribute('data-save-url'), {
        method: 'POST',
        headers: headers,
        body: JSON.stringify(ids)
      }).then(function (response) {
        if (!response.ok) throw new Error('save failed');
        lastSavedOrder = key;
        if (status) status.textContent = '排序已保存。';
      }).catch(function () {
        if (status) status.textContent = '排序保存失败，请刷新后重试。';
      }).then(function () {
        if (inFlightOrder === key) inFlightOrder = null;
        if (saveButton) saveButton.disabled = false;
      });
    }

    function saveIfChanged() {
      if (!startOrder || orderKey(startOrder) === orderKey(getOrder())) return;
      saveOrder(false);
    }

    list.addEventListener('dragstart', function (event) {
      var item = event.target.closest('[data-artist-id]');
      if (!item) return;
      dragging = item;
      startOrder = getOrder();
      item.setAttribute('data-dragging', 'true');
      if (event.dataTransfer) {
        event.dataTransfer.effectAllowed = 'move';
        // Firefox 需要 setData 才会开始拖拽。
        event.dataTransfer.setData('text/plain', item.getAttribute('data-artist-id'));
      }
    });

    list.addEventListener('dragover', function (event) {
      event.preventDefault();
      var item = event.target.closest('[data-artist-id]');
      if (!item || !dragging || item === dragging) return;

      var bounds = item.getBoundingClientRect();
      var insertBefore = event.clientY < bounds.top + bounds.height / 2 ? item : item.nextSibling;
      list.insertBefore(dragging, insertBefore);
      renumber();
    });

    list.addEventListener('drop', function (event) {
      event.preventDefault();
      saveIfChanged();
    });

    list.addEventListener('dragend', function () {
      if (dragging) dragging.removeAttribute('data-dragging');
      saveIfChanged();
      dragging = null;
      startOrder = null;
    });

    // ▲▼：触屏没有 HTML5 拖拽，也方便键盘操作；移动后同样立即保存。
    list.addEventListener('click', function (event) {
      var button = event.target.closest('[data-vip-move]');
      if (!button) return;
      var row = button.closest('[data-artist-id]');
      if (!row) return;

      if (button.getAttribute('data-vip-move') === 'up' && row.previousElementSibling) {
        list.insertBefore(row, row.previousElementSibling);
      } else if (button.getAttribute('data-vip-move') === 'down' && row.nextElementSibling) {
        list.insertBefore(row.nextElementSibling, row);
      } else {
        return;
      }

      renumber();
      saveOrder(false);
    });

    if (saveButton) saveButton.addEventListener('click', function () { saveOrder(true); });

    renumber();
    lastSavedOrder = orderKey(getOrder());
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
