// 现场编辑（T6）：仅在后台开启编辑态时由 _LiveEditAssets 加载。
// 带 data-le-site（站点文案）或 data-le-entity（数据库字段）的元素可点击编辑：
//   纯文本 → 就地编辑，Enter 保存（多行用 Ctrl/⌘+Enter），Esc 取消，点别处自动保存；
//   富文本 → 弹出编辑框（TipTap）。保存后立即生效，并写入后台操作记录。
(function () {
  'use strict';

  var SELECTOR = '[data-le-site],[data-le-entity]';
  var token = (document.querySelector('[data-live-edit-token] input[name="__RequestVerificationToken"]') || {}).value || '';
  var editing = null;

  // ---- 顶部提示条 ----
  var bar = document.createElement('div');
  bar.className = 'le-bar';
  bar.innerHTML = '<span class="le-bar__title">现场编辑中</span>' +
    '<span class="le-bar__hint">点虚线框文字修改、拖 ⠿ 排序；保存后立即生效，访客刷新即可看到</span>' +
    '<span class="le-bar__status" aria-live="polite"></span>' +
    '<a class="le-bar__exit" href="/Staff/LiveEdit/Exit" target="_top">退出编辑</a>';
  document.body.appendChild(bar);
  var statusEl = bar.querySelector('.le-bar__status');
  function status(text, isError) {
    statusEl.textContent = text;
    statusEl.classList.toggle('is-error', !!isError);
    if (!isError && text) setTimeout(function () { if (statusEl.textContent === text) statusEl.textContent = ''; }, 2500);
  }

  // 告知外层「现场编辑」后台页当前地址，便于显示与刷新。
  try { if (window.top !== window) window.top.postMessage({ type: 'szaipa-live-edit', url: location.pathname + location.search }, location.origin); } catch (e) { /* 跨域忽略 */ }

  function describe(el) {
    return el.hasAttribute('data-le-site')
      ? { kind: 'site', key: el.getAttribute('data-le-site') }
      : { kind: 'field', entity: el.getAttribute('data-le-entity'), id: Number(el.getAttribute('data-le-id')), field: el.getAttribute('data-le-field') };
  }

  function save(el, value) {
    var target = describe(el);
    var body = {
      kind: target.kind,
      key: target.key,
      entity: target.entity,
      id: target.id,
      field: target.field,
      textKind: el.getAttribute('data-le-kind') || 'text',
      value: value
    };
    status('保存中…');
    return fetch('/Staff/LiveEdit/Save', {
      method: 'POST',
      credentials: 'same-origin',
      headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
      body: JSON.stringify(body)
    })
      .then(function (response) {
        if (response.status === 401 || response.redirected) throw new Error('登录已过期，请重新登录后台');
        return response.json();
      })
      .then(function (data) {
        if (!data.success) throw new Error(data.error || '保存失败');
        status('已保存');
        return data.value;
      });
  }

  function render(el, value, kind) {
    if (kind === 'html') { el.innerHTML = value; return; }
    if (el.hasAttribute('data-le-value')) el.setAttribute('data-le-value', value);
    el.textContent = '';
    value.split('\n').forEach(function (line, index) {
      if (index > 0) el.appendChild(document.createElement('br'));
      el.appendChild(document.createTextNode(line));
    });
  }

  // ---- 纯文本就地编辑 ----
  function startInline(el) {
    var kind = el.getAttribute('data-le-kind') || 'text';
    var original = el.innerHTML;
    var originalText = el.hasAttribute('data-le-value') ? el.getAttribute('data-le-value') : el.innerText;
    // 多行内容先转成纯文本；保留可见换行，避免编辑时把原有链接或段落标签当作正文。
    if (kind === 'lines') el.textContent = originalText;
    editing = el;
    el.classList.add('le-editing');
    el.setAttribute('contenteditable', 'plaintext-only');
    if (el.contentEditable !== 'plaintext-only') el.setAttribute('contenteditable', 'true');
    el.focus();
    var range = document.createRange();
    range.selectNodeContents(el);
    var selection = window.getSelection();
    selection.removeAllRanges();
    selection.addRange(range);

    function finish(commit) {
      // 在移除保留换行的样式之前读取，否则 innerText 会把换行折叠为空格。
      var text = el.innerText.replace(/ /g, ' ');
      el.removeEventListener('keydown', onKey);
      el.removeEventListener('blur', onBlur);
      el.removeAttribute('contenteditable');
      el.classList.remove('le-editing');
      editing = null;
      if (!commit || text === originalText) { el.innerHTML = original; return; }
      el.classList.add('le-saving');
      save(el, text)
        .then(function (value) { render(el, value, kind); })
        .catch(function (error) { el.innerHTML = original; status(error.message, true); })
        .then(function () { el.classList.remove('le-saving'); });
    }
    function onKey(event) {
      if (event.key === 'Escape') { event.preventDefault(); finish(false); }
      else if (event.key === 'Enter' && (kind === 'text' || event.metaKey || event.ctrlKey)) { event.preventDefault(); finish(true); }
    }
    function onBlur() { finish(true); }
    el.addEventListener('keydown', onKey);
    el.addEventListener('blur', onBlur);
  }

  // ---- 富文本弹窗 ----
  function startRich(el) {
    if (!window.SzaipaEditor || !window.SzaipaEditor.mount) { status('富文本编辑器未加载', true); return; }
    var overlay = document.createElement('div');
    overlay.className = 'le-modal';
    overlay.innerHTML = '<div class="le-modal__panel" role="dialog" aria-modal="true">' +
      '<div class="le-modal__head"><strong>编辑正文</strong><span class="le-modal__tip">图片可在工具栏「图片」上传</span></div>' +
      '<div class="admin-editor" data-editor data-field="le-rich" data-upload-url="/Staff/Upload/Image?folder=newsImg">' +
      '<div class="admin-editor__toolbar" data-editor-toolbar></div><div class="admin-editor__surface" data-editor-surface></div>' +
      '<input type="hidden" id="le-rich"></div>' +
      '<div class="le-modal__foot"><button type="button" class="le-btn" data-le-cancel>取消</button>' +
      '<button type="button" class="le-btn le-btn--primary" data-le-save>保存</button></div></div>';
    overlay.querySelector('#le-rich').value = el.innerHTML;
    // 上传图片需要防伪令牌：编辑器从所在表单或页面里找 __RequestVerificationToken。
    document.body.appendChild(overlay);
    var root = overlay.querySelector('[data-editor]');
    var editor = window.SzaipaEditor.mount(root);
    function close() { if (editor) editor.destroy(); overlay.remove(); }
    overlay.querySelector('[data-le-cancel]').addEventListener('click', close);
    overlay.addEventListener('keydown', function (event) { if (event.key === 'Escape') close(); });
    overlay.querySelector('[data-le-save]').addEventListener('click', function () {
      var html = editor ? editor.getHTML() : overlay.querySelector('#le-rich').value;
      save(el, html)
        .then(function (value) { el.innerHTML = value; close(); })
        .catch(function (error) { status(error.message, true); });
    });
  }

  // ---- 列表拖拽排序 + 点击新增（首页核心成员 / 顾问团队）----
  // 容器带 data-le-sortable（实体）与 data-le-group（分组），每项带 data-le-sort-id。
  // 用 Pointer 事件实现，鼠标与手指都能拖；松手后把整组新顺序提交给 /Staff/LiveEdit/Reorder。
  function postJson(url, body) {
    return fetch(url, {
      method: 'POST',
      credentials: 'same-origin',
      headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
      body: JSON.stringify(body)
    }).then(function (response) {
      if (response.status === 401 || response.redirected) throw new Error('登录已过期，请重新登录后台');
      return response.json();
    }).then(function (data) {
      if (!data.success) throw new Error(data.error || '保存失败');
      return data;
    });
  }

  function sortIds(container) {
    return Array.prototype.map.call(container.querySelectorAll(':scope > [data-le-sort-id]'), function (item) {
      return Number(item.getAttribute('data-le-sort-id'));
    });
  }

  function setupSortable(container) {
    var entity = container.getAttribute('data-le-sortable');
    Array.prototype.forEach.call(container.querySelectorAll(':scope > [data-le-sort-id]'), function (item) {
      item.classList.add('le-sortable-item');
      var handle = document.createElement('button');
      handle.type = 'button';
      handle.className = 'le-drag-handle';
      handle.title = '按住拖动调整顺序';
      handle.setAttribute('aria-label', '拖动排序');
      handle.textContent = '⠿';
      item.appendChild(handle);

      handle.addEventListener('click', function (event) { event.preventDefault(); event.stopPropagation(); });
      handle.addEventListener('pointerdown', function (event) {
        event.preventDefault();
        event.stopPropagation();
        var before = sortIds(container).join(',');
        var pointerId = event.pointerId;
        item.classList.add('le-dragging');
        // 拖动时卡片会在 DOM 中换位置，浏览器会因此释放 pointer capture；
        // 所以移动 / 松手监听挂在 window 上，而不是手柄。

        function onMove(moveEvent) {
          if (moveEvent.pointerId !== pointerId) return;
          moveEvent.preventDefault();
          var under = document.elementFromPoint(moveEvent.clientX, moveEvent.clientY);
          var target = under && under.closest && under.closest('[data-le-sort-id]');
          if (!target || target === item || target.parentElement !== container) return;
          var rect = target.getBoundingClientRect();
          var after = moveEvent.clientX > rect.left + rect.width / 2;
          container.insertBefore(item, after ? target.nextSibling : target);
        }
        function onUp(upEvent) {
          if (upEvent.pointerId !== pointerId) return;
          window.removeEventListener('pointermove', onMove);
          window.removeEventListener('pointerup', onUp);
          window.removeEventListener('pointercancel', onUp);
          item.classList.remove('le-dragging');
          var ids = sortIds(container);
          if (ids.join(',') === before) return;
          status('保存排序…');
          postJson('/Staff/LiveEdit/Reorder', { entity: entity, ids: ids })
            .then(function () { status('排序已保存'); })
            .catch(function (error) { status(error.message, true); setTimeout(function () { location.reload(); }, 1500); });
        }
        window.addEventListener('pointermove', onMove, { passive: false });
        window.addEventListener('pointerup', onUp);
        window.addEventListener('pointercancel', onUp);
      });
    });

    if (entity === 'OrgMember') {
      var add = document.createElement('button');
      add.type = 'button';
      add.className = 'le-add-tile';
      add.innerHTML = '<span class="le-add-tile__plus">＋</span><span>新增成员</span>';
      add.addEventListener('click', function (event) {
        event.preventDefault();
        event.stopPropagation();
        openMemberDialog(container.getAttribute('data-le-group'));
      });
      container.appendChild(add);
    }
  }

  var memberOptions = null;
  function loadMemberOptions() {
    if (memberOptions) return Promise.resolve(memberOptions);
    return fetch('/Staff/LiveEdit/OrgMemberOptions', { credentials: 'same-origin' })
      .then(function (response) { return response.json(); })
      .then(function (data) { memberOptions = data; return data; });
  }

  function openMemberDialog(group) {
    var overlay = document.createElement('div');
    overlay.className = 'le-modal';
    overlay.innerHTML = '<form class="le-modal__panel le-member-form" role="dialog" aria-modal="true">' +
      '<div class="le-modal__head"><strong>新增成员 · ' + group + '</strong><span class="le-modal__tip">加在本组最后，之后可拖动调整</span></div>' +
      '<div class="le-member-form__body">' +
      '<div class="le-member-form__avatar"><img data-avatar-preview alt="" hidden><span data-avatar-empty>头像</span></div>' +
      '<div class="le-member-form__fields">' +
      '<label>关联艺术家（可选，选了自动带出姓名和头像）<select name="artistId"><option value="">不关联</option></select></label>' +
      '<label>姓名<input name="name" maxlength="50" autocomplete="off"></label>' +
      '<label>职务<input name="role" list="le-org-roles" placeholder="如 副会长" autocomplete="off"></label>' +
      '<label>头衔<input name="title" list="le-org-titles" placeholder="如 艺术家" autocomplete="off"></label>' +
      '<label>上传头像（不选则用艺术家头像）<input name="file" type="file" accept="image/*"></label>' +
      '<datalist id="le-org-roles"></datalist><datalist id="le-org-titles"></datalist>' +
      '</div></div>' +
      '<div class="le-modal__foot"><span class="le-member-form__error" aria-live="polite"></span>' +
      '<button type="button" class="le-btn" data-le-cancel>取消</button>' +
      '<button type="submit" class="le-btn le-btn--primary">添加</button></div></form>';
    document.body.appendChild(overlay);
    var form = overlay.querySelector('form');
    var preview = form.querySelector('[data-avatar-preview]');
    var empty = form.querySelector('[data-avatar-empty]');
    var errorEl = form.querySelector('.le-member-form__error');
    var uploaded = null; // { url, width, height }
    var artistAvatars = {};

    function showAvatar(url) {
      if (url) { preview.src = url; preview.hidden = false; empty.hidden = true; }
      else { preview.hidden = true; empty.hidden = false; }
    }
    function close() { overlay.remove(); }
    overlay.querySelector('[data-le-cancel]').addEventListener('click', close);
    overlay.addEventListener('keydown', function (event) { if (event.key === 'Escape') close(); });

    loadMemberOptions().then(function (data) {
      var select = form.elements.artistId;
      data.artists.forEach(function (artist) {
        var option = document.createElement('option');
        option.value = artist.id;
        option.textContent = artist.name;
        select.appendChild(option);
        artistAvatars[artist.id] = artist;
      });
      [['le-org-roles', data.roles], ['le-org-titles', data.titles]].forEach(function (pair) {
        var list = form.querySelector('#' + pair[0]);
        pair[1].forEach(function (value) { var option = document.createElement('option'); option.value = value; list.appendChild(option); });
      });
    }).catch(function () { errorEl.textContent = '艺术家列表加载失败，可直接填写姓名'; });

    form.elements.artistId.addEventListener('change', function () {
      var artist = artistAvatars[this.value];
      if (artist && !form.elements.name.value.trim()) form.elements.name.value = artist.name;
      if (!uploaded) showAvatar(artist ? artist.avatar : null);
    });

    form.elements.file.addEventListener('change', function () {
      var file = this.files && this.files[0];
      if (!file) return;
      errorEl.textContent = '头像上传中…';
      var data = new FormData();
      data.append('file', file);
      data.append('__RequestVerificationToken', token);
      fetch('/Staff/Upload/Image?folder=ArtImg/Org', { method: 'POST', credentials: 'same-origin', body: data })
        .then(function (response) { return response.json(); })
        .then(function (result) {
          if (!result.success) throw new Error(result.error || '上传失败');
          var probe = new Image();
          probe.onload = function () { uploaded = { url: result.url, width: probe.naturalWidth, height: probe.naturalHeight }; };
          probe.src = result.url;
          uploaded = { url: result.url };
          showAvatar(result.url);
          errorEl.textContent = '';
        })
        .catch(function (error) { errorEl.textContent = error.message; });
    });

    form.addEventListener('submit', function (event) {
      event.preventDefault();
      var artistId = form.elements.artistId.value ? Number(form.elements.artistId.value) : null;
      var name = form.elements.name.value.trim();
      if (!name && !artistId) { errorEl.textContent = '请填写姓名或选择艺术家'; return; }
      errorEl.textContent = '保存中…';
      postJson('/Staff/LiveEdit/OrgMember/Create', {
        group: group,
        name: name,
        role: form.elements.role.value.trim(),
        title: form.elements.title.value.trim(),
        artistId: artistId,
        avatarPath: uploaded ? uploaded.url : null,
        imageWidth: uploaded && uploaded.width || null,
        imageHeight: uploaded && uploaded.height || null
      }).then(function () {
        errorEl.textContent = '已添加，正在刷新…';
        location.reload();
      }).catch(function (error) { errorEl.textContent = error.message; });
    });
  }

  Array.prototype.forEach.call(document.querySelectorAll('[data-le-sortable]'), setupSortable);

  // 捕获阶段拦截：可编辑元素（含在链接里的）点击只进入编辑，不跳转。
  document.addEventListener('click', function (event) {
    var el = event.target.closest && event.target.closest(SELECTOR);
    if (!el || el === editing || bar.contains(el)) return;
    event.preventDefault();
    event.stopPropagation();
    if (editing) editing.blur();
    if (el.getAttribute('data-le-kind') === 'html') startRich(el); else startInline(el);
  }, true);
})();
