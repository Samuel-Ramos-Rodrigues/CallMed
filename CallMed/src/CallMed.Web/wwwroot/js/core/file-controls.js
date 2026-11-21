(() => {
  'use strict';
  let nextId=0;
  function enhance(root){
    for(const input of root.querySelectorAll('input[type=file]:not([data-file-control]):not([hidden]):not(.cm-account-file)')){
      input.dataset.fileControl='true';
      const wrapper=document.createElement('span'); wrapper.className='cm-file-control';
      input.before(wrapper); wrapper.append(input); input.classList.add('cm-file-native');
      if(!input.id) input.id='cm-upload-'+(++nextId);
      const button=document.createElement('span');button.className='cm-file-button';button.textContent='Escolher arquivo';
      const name=document.createElement('span');name.className='cm-file-name';name.textContent='Nenhum arquivo selecionado';
      name.id=input.id+'-filename';name.setAttribute('aria-live','polite');
      input.setAttribute('aria-describedby',[input.getAttribute('aria-describedby'),name.id].filter(Boolean).join(' '));
      wrapper.append(button,name);
      input.addEventListener('change',()=>{name.textContent=[...input.files].map(f=>f.name).join(', ')||'Nenhum arquivo selecionado';});
    }
  }
  enhance(document);
  new MutationObserver(records=>{if(records.some(r=>[...r.addedNodes].some(n=>n.nodeType===1&&(n.matches?.('input[type=file]')||n.querySelector?.('input[type=file]')))))enhance(document);}).observe(document.body,{childList:true,subtree:true});
})();
