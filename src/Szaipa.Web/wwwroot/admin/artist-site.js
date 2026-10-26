(function () {
  'use strict';

  function wireBannerPreview() {
    var preview = document.getElementById('artist-banner-preview');
    var firstColor = document.getElementById('preview-color1');
    var secondColor = document.getElementById('preview-color2');
    if (!preview || !firstColor || !secondColor) return;

    function syncColors() {
      var bannerUrl = preview.getAttribute('data-banner-url');
      preview.style.backgroundImage = 'linear-gradient(120deg,' + firstColor.value + ',' +
        secondColor.value + '),url(' + JSON.stringify(bannerUrl) + ')';
    }

    firstColor.addEventListener('input', syncColors);
    secondColor.addEventListener('input', syncColors);
  }

  function wireWorkSorting() {
    var list = document.getElementById('artist-work-list');
    if (!list || !list.getAttribute('data-reorder-url')) return;

    var dragging = null;
    var startOrder = null;
    var lastSavedOrder = null;
    var inFlightOrder = null;
    var status = document.querySelector('[data-work-order-status]');
    var saveButton = document.querySelector('[data-save-work-order]');

    function getOrder() {
      return Array.prototype.map.call(list.querySelectorAll('[data-work-id]'), function (item) {
        return Number(item.getAttribute('data-work-id'));
      });
    }

    function orderKey(workIds) {
      return workIds.join(',');
    }

    function saveOrder(force) {
      var workIds = getOrder();
      var key = orderKey(workIds);
      if (!force && (key === lastSavedOrder || key === inFlightOrder)) return;

      var tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
      var headers = { 'Content-Type': 'application/json' };
      if (tokenInput) headers.RequestVerificationToken = tokenInput.value;
      inFlightOrder = key;
      if (status) status.textContent = '排序保存中…';
      if (saveButton) saveButton.disabled = true;

      fetch(list.getAttribute('data-reorder-url'), {
        method: 'POST',
        headers: headers,
        body: JSON.stringify(workIds)
      }).then(function (response) {
        if (!response.ok) throw new Error('保存失败，请刷新后重试。');
        lastSavedOrder = key;
        if (status) status.textContent = '排序已保存。';
      }).catch(function () {
        if (status) status.textContent = '排序保存失败，请重试。';
      }).then(function () {
        if (inFlightOrder === key) inFlightOrder = null;
        if (saveButton) saveButton.disabled = false;
      });
    }

    list.addEventListener('dragstart', function (event) {
      var item = event.target.closest('[data-work-id]');
      if (!item) return;
      dragging = item;
      startOrder = getOrder();
    });

    list.addEventListener('dragover', function (event) {
      event.preventDefault();
      var item = event.target.closest('[data-work-id]');
      if (!item || !dragging || item === dragging) return;

      var bounds = item.getBoundingClientRect();
      var insertBefore = event.clientY < bounds.top + bounds.height / 2 ? item : item.nextSibling;
      list.insertBefore(dragging, insertBefore);
    });

    function saveChangedOrder() {
      var workIds = getOrder();
      if (!startOrder || orderKey(startOrder) === orderKey(workIds)) return;
      saveOrder(false);
    }

    list.addEventListener('drop', function (event) {
      event.preventDefault();
      saveChangedOrder();
    });

    list.addEventListener('dragend', function () {
      saveChangedOrder();
      dragging = null;
      startOrder = null;
    });

    if (saveButton) saveButton.addEventListener('click', function () { saveOrder(true); });
  }

  function wirePublicationOrdering() {
    document.querySelectorAll('[data-move]').forEach(function (button) {
      button.addEventListener('click', function () {
        var row = button.closest('[data-publication-row]');
        if (!row) return;

        var parent = row.parentElement;
        if (button.getAttribute('data-move') === 'up' && row.previousElementSibling) {
          parent.insertBefore(row, row.previousElementSibling);
        }
        if (button.getAttribute('data-move') === 'down' && row.nextElementSibling) {
          parent.insertBefore(row.nextElementSibling, row);
        }
      });
    });
  }

  function init() {
    wireBannerPreview();
    wireWorkSorting();
    wirePublicationOrdering();
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', init);
  } else {
    init();
  }
})();
