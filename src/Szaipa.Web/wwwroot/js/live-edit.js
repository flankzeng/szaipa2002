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
    '<span class="le-bar__hint">点击带虚线框的文字即可修改</span>' +
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
    var originalText = el.innerText;
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
      el.removeEventListener('keydown', onKey);
      el.removeEventListener('blur', onBlur);
      el.removeAttribute('contenteditable');
      el.classList.remove('le-editing');
      editing = null;
      var text = el.innerText.replace(/ /g, ' ');
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
