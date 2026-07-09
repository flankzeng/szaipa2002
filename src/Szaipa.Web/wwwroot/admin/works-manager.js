// Works-catalog manager for the exhibition admin (参展作品目录). Plain JS, no bundling — sibling of
// gallery-manager.js but each row is a full record (category/title/artist/size/medium/image) instead of a
// bare numbered file. Unlike the gallery, each work's image is a normal single-image upload (unique GUID
// name via the generic /Staff/Upload/Image endpoint) — no gap-free renumbering needed.
// Markup: a [data-works] container with data-upload-url, data-list-url, data-publication-id, a hidden input
// (the JSON-encoded ordered rows), a [data-works-list] target and a [data-works-add] button.
(function () {
  'use strict';

  function token(scope) {
    var i = (scope.closest('form') || document).querySelector('input[name="__RequestVerificationToken"]');
    return i ? i.value : '';
  }

  var FIELDS = [
    { key: 'category', placeholder: '分类' },
    { key: 'title', placeholder: '标题' },
    { key: 'artist', placeholder: '艺术家' },
    { key: 'size', placeholder: '尺寸' },
    { key: 'medium', placeholder: '材质' }
  ];

  function wire(root) {
    if (root.__wired) return;
    root.__wired = true;

    var uploadUrl = root.getAttribute('data-upload-url');
    var listUrl = root.getAttribute('data-list-url');
    var publicationId = parseInt(root.getAttribute('data-publication-id') || '0', 10) || 0;
    var hidden = root.querySelector('input[type="hidden"]');
    var list = root.querySelector('[data-works-list]');
    var addBtn = root.querySelector('[data-works-add]');
    var status = root.querySelector('[data-works-status]');
    var folderInput = document.querySelector(root.getAttribute('data-folder-input') || '[name="FolderName"]');

    var items = []; // { category, title, artist, size, medium, imagePath }

    function folder() { return folderInput ? (folderInput.value || '').trim() : ''; }

    function render() {
      list.innerHTML = '';
      items.forEach(function (it, idx) {
        var row = document.createElement('div');
        row.className = 'works-row';

        var img = document.createElement('div');
        img.className = 'works-row__img';
        img.innerHTML =
          (it.imagePath ? '<img src="' + it.imagePath + '" alt="" />' : '<div class="works-row__placeholder">无图</div>') +
          '<label class="works-row__upload">上传<input type="file" accept="image/*" class="hidden" data-work-image /></label>';
        img.querySelector('[data-work-image]').addEventListener('change', function (e) {
          var file = e.target.files && e.target.files[0];
          if (file) uploadImage(idx, file);
        });
        row.appendChild(img);

        var fields = document.createElement('div');
        fields.className = 'works-row__fields';
        FIELDS.forEach(function (f) {
          var input = document.createElement('input');
          input.type = 'text';
          input.className = 'admin-input';
          input.placeholder = f.placeholder;
          input.value = it[f.key] || '';
          input.addEventListener('input', function () { it[f.key] = input.value; syncHidden(); });
          fields.appendChild(input);
        });
        row.appendChild(fields);

        var actions = document.createElement('div');
        actions.className = 'works-row__actions';
        actions.innerHTML =
          '<button type="button" title="前移" data-act="up">↑</button>' +
          '<button type="button" title="后移" data-act="down">↓</button>' +
          '<button type="button" title="删除" data-act="del">✕</button>';
        actions.querySelector('[data-act="up"]').onclick = function () { move(idx, -1); };
        actions.querySelector('[data-act="down"]').onclick = function () { move(idx, 1); };
        actions.querySelector('[data-act="del"]').onclick = function () { items.splice(idx, 1); render(); };
        row.appendChild(actions);

        list.appendChild(row);
      });
      syncHidden();
      if (status) status.textContent = items.length + ' 件作品';
    }

    function syncHidden() { hidden.value = JSON.stringify(items); }

    function move(idx, delta) {
      var to = idx + delta;
      if (to < 0 || to >= items.length) return;
      var tmp = items[idx]; items[idx] = items[to]; items[to] = tmp;
      render();
    }

    function uploadImage(idx, file) {
      var f = folder();
      if (!f) { if (status) status.textContent = '请先填写图片文件夹名'; return; }
      var form = new FormData();
      form.append('file', file);
      form.append('folder', f + '/works');
      var t = token(root);
      if (t) form.append('__RequestVerificationToken', t);
      if (status) status.textContent = '上传中…';
      fetch(uploadUrl, { method: 'POST', body: form })
        .then(function (r) { return r.json(); })
        .then(function (data) {
          if (data.success) { items[idx].imagePath = data.url; render(); }
          else if (status) { status.textContent = data.error || '上传失败'; }
        });
    }

    if (addBtn) {
      addBtn.addEventListener('click', function () {
        items.push({ category: '', title: '', artist: '', size: '', medium: '', imagePath: '' });
        render();
      });
    }

    // Edit page: load the existing works catalog for this publication.
    if (listUrl && publicationId > 0) {
      fetch(listUrl + '/' + publicationId)
        .then(function (r) { return r.json(); })
        .then(function (rows) {
          items = (rows || []).map(function (r) {
            return { category: r.category, title: r.title, artist: r.artist, size: r.size, medium: r.medium, imagePath: r.imagePath };
          });
          render();
        });
    } else {
      render();
    }

    // Keep the hidden value current on submit.
    var form = root.closest('form');
    if (form) form.addEventListener('submit', syncHidden);
  }

  function init(scope) { (scope || document).querySelectorAll('[data-works]').forEach(wire); }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', function () { init(); });
  } else { init(); }

  window.SzaipaWorks = { init: init };
})();
