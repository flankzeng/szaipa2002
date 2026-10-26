(() => {
  const form = document.querySelector('[data-block-manager]');
  if (!form) return;
  const hidden = form.querySelector('input[name="BlocksJson"]');
  const list = form.querySelector('[data-block-list]');
  const typeSelect = form.querySelector('[data-block-type]');
  const typeInput = document.querySelector('[name="Type"]');
  const topicPanel = document.querySelector('[data-topic-editor]');
  let blocks = [];
  try { blocks = JSON.parse(hidden.value || '[]'); } catch { blocks = []; }
  const fields = { banner: [['title','标题'],['subtitle','副标题'],['imagePath','图片路径']], preface: [['heading','标题'],['body','正文'],['signature','落款']], richtext: [['heading','标题'],['html','富文本 HTML']], works: [['heading','标题']], gallery: [['heading','标题']], video: [['source','来源 local/embed'],['url','视频地址'],['poster','封面路径'],['caption','说明']], quote: [['text','引言'],['author','作者']] };
  const sync = () => { hidden.value = JSON.stringify(blocks); };
  const flushEditors = () => list.querySelectorAll('[data-block-html]').forEach(input => {
    const index = Number(input.dataset.blockIndex);
    const editor = input.closest('[data-editor]')?.__editor;
    if (blocks[index]) blocks[index].content.html = editor?.getHTML() ?? input.value;
  });
  const destroyEditors = () => list.querySelectorAll('[data-editor]').forEach(root => {
    root.__editor?.destroy();
    root.__editor = null;
  });
  document.querySelector('form').addEventListener('submit', () => {
    flushEditors();
    sync();
  });
  const render = () => {
    destroyEditors();
    list.replaceChildren();
    blocks.forEach((block, index) => {
      const card = document.createElement('section'); card.className = 'topic-block-card';
      const header = document.createElement('div'); header.className = 'mb-3 flex items-center justify-between';
      const title = document.createElement('strong'); title.textContent = `${index + 1}. ${block.type}`; header.append(title);
      const actions = document.createElement('div'); actions.className='topic-block-card__actions';
      [['↑',-1],['↓',1],['删除',0]].forEach(([label,dir])=>{const button=document.createElement('button');button.type='button';button.className='admin-btn-ghost px-2 py-1';button.textContent=label;button.addEventListener('click',()=>{flushEditors();if(dir===0)blocks.splice(index,1);else{const to=index+dir;if(to<0||to>=blocks.length)return;[blocks[index],blocks[to]]=[blocks[to],blocks[index]];}sync();render();});actions.append(button);}); header.append(actions); card.append(header);
      (fields[block.type]||[]).forEach(([key,label])=>{
        const wrap=document.createElement('label');wrap.className='mb-3 block text-sm';wrap.textContent=label;
        if(block.type==='richtext'&&key==='html'){
          const id=`topic-richtext-${index}`; const root=document.createElement('div'); root.className='admin-editor mt-1'; root.dataset.editor=''; root.dataset.field=id; root.dataset.uploadUrl='/Staff/Upload/Image?folder=Publication';
          const toolbar=document.createElement('div'); toolbar.className='admin-editor__toolbar'; toolbar.dataset.editorToolbar='';
          const surface=document.createElement('div'); surface.className='admin-editor__surface'; surface.dataset.editorSurface='';
          const input=document.createElement('input'); input.type='hidden'; input.id=id; input.dataset.blockHtml=''; input.dataset.blockIndex=String(index); input.value=block.content?.html||'';
          root.append(toolbar,surface,input); wrap.append(root); card.append(wrap);
          if(window.SzaipaEditor?.mount) root.__editor = window.SzaipaEditor.mount(root);
          return;
        }
        const input=key==='body'?document.createElement('textarea'):document.createElement('input');input.className='admin-input mt-1';if(input.tagName==='TEXTAREA')input.rows=5;input.value=block.content?.[key]||'';input.addEventListener('input',()=>{block.content[key]=input.value;sync();});wrap.append(input);card.append(wrap);
      });
      list.append(card);
    }); sync();
  };
  form.querySelector('[data-block-add]').addEventListener('click',()=>{flushEditors();const type=typeSelect.value;blocks.push({type,content:{}});render();});
  const updateVisibility=()=>{topicPanel.hidden=Number(typeInput.value)!==2;};
  typeInput.addEventListener('change',updateVisibility);updateVisibility();render();
})();
