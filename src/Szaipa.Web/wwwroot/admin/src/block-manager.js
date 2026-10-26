// 专题积木编辑器：把积木数组序列化进隐藏字段 BlocksJson，随展览表单一起提交。
(() => {
  const form = document.querySelector('[data-block-manager]');
  if (!form) return;
  const hidden = form.querySelector('input[name="BlocksJson"]');
  const list = form.querySelector('[data-block-list]');
  const typeSelect = form.querySelector('[data-block-type]');
  const typeInput = document.querySelector('[name="Type"]');
  const topicPanel = document.querySelector('[data-topic-editor]');
  const importantOnly = document.querySelectorAll('[data-important-only]');
  const uploadFolder = 'Publication';
  let blocks = [];
  try { blocks = JSON.parse(hidden.value || '[]'); } catch { blocks = []; }

  const typeNames = { banner: '横幅', preface: '序言', richtext: '富文本', works: '作品目录', gallery: '图库', video: '视频', quote: '引言' };
  const typeHints = {
    works: '展示本页下方「参展作品目录」里录入的作品。',
    gallery: '展示本页下方「展览图片」里上传的图片。'
  };
  // 字段：[键, 标签, 控件类型]；image = 路径输入框 + 上传按钮。
  const fields = {
    banner: [['title', '标题'], ['subtitle', '副标题'], ['imagePath', '横幅图片', 'image']],
    preface: [['heading', '标题'], ['body', '正文', 'textarea'], ['signature', '落款']],
    richtext: [['heading', '标题'], ['html', '正文', 'richtext']],
    works: [['heading', '标题']],
    gallery: [['heading', '标题']],
    video: [['source', '视频来源', 'videoSource'], ['url', '视频地址'], ['poster', '封面图片', 'image'], ['caption', '说明']],
    quote: [['text', '引言', 'textarea'], ['author', '作者']]
  };

  const sync = () => { hidden.value = JSON.stringify(blocks); };
  const flushEditors = () => list.querySelectorAll('[data-block-html]').forEach(input => {
    const index = Number(input.dataset.blockIndex);
    const editor = input.closest('[data-editor]')?.__editor;
    // 去掉首尾的空段落（回车留下的 <p></p>），避免前台正文顶部出现空行。
    const html = (editor?.getHTML() ?? input.value).replace(/^(?:<p><\/p>)+|(?:<p><\/p>)+$/g, '');
    if (blocks[index]) blocks[index].content.html = html;
  });
  const destroyEditors = () => list.querySelectorAll('[data-editor]').forEach(root => {
    root.__editor?.destroy();
    root.__editor = null;
  });

  const requestToken = () => document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';

  // 给一个路径输入框加「上传图片」按钮：上传成功后把返回的 /Content/... 地址写回输入框并触发 input。
  const attachUploader = (input, wrap) => {
    const row = document.createElement('div'); row.className = 'mt-1 flex flex-wrap items-center gap-2';
    input.classList.add('flex-1'); input.placeholder = '/Content/... 或点右侧上传';
    const button = document.createElement('button'); button.type = 'button'; button.className = 'admin-btn-ghost px-3 py-1'; button.textContent = '上传图片';
    const status = document.createElement('span'); status.className = 'text-xs text-muted';
    const preview = document.createElement('img'); preview.className = 'mt-2 max-h-28 rounded border border-black/10'; preview.alt = '';
    const showPreview = () => { preview.hidden = !input.value; if (input.value) preview.src = input.value; };
    const picker = document.createElement('input'); picker.type = 'file'; picker.accept = 'image/*'; picker.hidden = true;
    button.addEventListener('click', () => picker.click());
    picker.addEventListener('change', () => {
      const file = picker.files?.[0]; picker.value = '';
      if (!file) return;
      const body = new FormData();
      body.append('file', file);
      body.append('folder', uploadFolder);
      body.append('__RequestVerificationToken', requestToken());
      status.textContent = '上传中…';
      fetch(`/Staff/Upload/Image?folder=${uploadFolder}`, { method: 'POST', body, headers: { 'X-Requested-With': 'fetch' } })
        .then(response => response.json())
        .then(data => {
          if (!data.success) throw new Error(data.error || '上传失败');
          input.value = data.url;
          input.dispatchEvent(new Event('input', { bubbles: true }));
          status.textContent = '已上传';
        })
        .catch(error => { status.textContent = error.message || '上传失败'; });
    });
    input.addEventListener('input', showPreview);
    row.append(input, button, status, picker);
    wrap.append(row, preview);
    showPreview();
  };

  document.querySelector('form').addEventListener('submit', () => {
    flushEditors();
    sync();
  });

  const render = () => {
    destroyEditors();
    list.replaceChildren();
    blocks.forEach((block, index) => {
      block.content ||= {};
      // 卡片先插入页面再填字段：富文本编辑器挂载时要能读到所在表单。
      const card = document.createElement('section'); card.className = 'topic-block-card'; list.append(card);
      const header = document.createElement('div'); header.className = 'mb-3 flex items-center justify-between';
      const title = document.createElement('strong'); title.textContent = `${index + 1}. ${typeNames[block.type] || block.type}`; header.append(title);
      const actions = document.createElement('div'); actions.className = 'topic-block-card__actions';
      [['↑', -1, '上移'], ['↓', 1, '下移'], ['删除', 0, '删除这块积木']].forEach(([label, dir, tip]) => {
        const button = document.createElement('button'); button.type = 'button'; button.className = 'admin-btn-ghost px-2 py-1'; button.textContent = label; button.title = tip;
        if ((dir === -1 && index === 0) || (dir === 1 && index === blocks.length - 1)) button.disabled = true;
        button.addEventListener('click', () => {
          flushEditors();
          if (dir === 0) {
            if (!confirm(`确定删除第 ${index + 1} 块「${typeNames[block.type] || block.type}」？保存后生效。`)) return;
            blocks.splice(index, 1);
          } else {
            const to = index + dir;
            if (to < 0 || to >= blocks.length) return;
            [blocks[index], blocks[to]] = [blocks[to], blocks[index]];
          }
          sync(); render();
        });
        actions.append(button);
      });
      header.append(actions); card.append(header);
      if (typeHints[block.type]) {
        const hint = document.createElement('p'); hint.className = 'mb-3 text-xs text-muted'; hint.textContent = typeHints[block.type]; card.append(hint);
      }

      (fields[block.type] || []).forEach(([key, label, kind]) => {
        const wrap = document.createElement('label'); wrap.className = 'mb-3 block text-sm'; wrap.textContent = label;
        if (kind === 'richtext') {
          const id = `topic-richtext-${index}`; const root = document.createElement('div'); root.className = 'admin-editor mt-1'; root.dataset.editor = ''; root.dataset.field = id; root.dataset.uploadUrl = `/Staff/Upload/Image?folder=${uploadFolder}`;
          const toolbar = document.createElement('div'); toolbar.className = 'admin-editor__toolbar'; toolbar.dataset.editorToolbar = '';
          const surface = document.createElement('div'); surface.className = 'admin-editor__surface'; surface.dataset.editorSurface = '';
          const input = document.createElement('input'); input.type = 'hidden'; input.id = id; input.dataset.blockHtml = ''; input.dataset.blockIndex = String(index); input.value = block.content.html || '';
          root.append(toolbar, surface, input); wrap.append(root); card.append(wrap);
          if (window.SzaipaEditor?.mount) root.__editor = window.SzaipaEditor.mount(root);
          return;
        }
        let input;
        if (kind === 'videoSource') {
          input = document.createElement('select');
          [['local', '本站视频文件（mp4）'], ['embed', '外部播放器链接（仅限白名单视频网站）']].forEach(([value, text]) => input.append(new Option(text, value)));
          input.value = block.content[key] || 'local';
          block.content[key] = input.value;
        } else {
          input = kind === 'textarea' ? document.createElement('textarea') : document.createElement('input');
          if (kind === 'textarea') input.rows = 5;
          input.value = block.content[key] || '';
        }
        input.className = 'admin-input mt-1';
        input.addEventListener(input.tagName === 'SELECT' ? 'change' : 'input', () => { block.content[key] = input.value; sync(); });
        if (kind === 'image') attachUploader(input, wrap); else wrap.append(input);
        card.append(wrap);
      });
    });
    if (blocks.length === 0) {
      const empty = document.createElement('p'); empty.className = 'text-sm text-muted'; empty.textContent = '还没有积木。在上方选择类型后点「添加积木」，按从上到下的顺序拼出专题页。';
      list.append(empty);
    }
    sync();
  };

  form.querySelector('[data-block-add]').addEventListener('click', () => {
    flushEditors();
    blocks.push({ type: typeSelect.value, content: {} });
    render();
    list.lastElementChild?.scrollIntoView({ behavior: 'smooth', block: 'center' });
  });

  // 专题页头部横幅也给上传按钮。
  const bannerInput = document.querySelector('input[name="BannerPath"]');
  if (bannerInput) {
    const holder = document.createElement('div');
    bannerInput.replaceWith(holder);
    attachUploader(bannerInput, holder);
  }

  // 类型切换：专题显示积木面板；「序」只对「重要」类型生效，专题用「序言」积木代替。
  const updateVisibility = () => {
    const type = Number(typeInput.value);
    topicPanel.hidden = type !== 2;
    importantOnly.forEach(el => { el.hidden = type === 2; });
  };
  typeInput.addEventListener('change', updateVisibility); updateVisibility(); render();
})();
