"""Restore reviewed garment regions by alpha only. Never redraw or recolor RGB."""
import argparse, hashlib, json, shutil
from pathlib import Path
import numpy as np
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parents[1]
REVIEW=ROOT/'.publish'/'repair-review'
SPEC=json.loads((Path(__file__).parent/'cutout-protection.json').read_text(encoding='utf8'))

def protection_mask(number, size):
    mask=Image.new('L',size)
    entry=SPEC['images'].get(f'{number:02}')
    if entry:
        draw=ImageDraw.Draw(mask);sw,sh=SPEC['coordinate_size'];w,h=size
        for polygon in entry['polygons']:
            draw.polygon([(round(x*w/sw),round(y*h/sh)) for x,y in polygon],fill=255)
    return np.array(mask)

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--apply',action='store_true');args=parser.parse_args()
    out=REVIEW/'fixed';before=REVIEW/'before';out.mkdir(parents=True,exist_ok=True);before.mkdir(exist_ok=True)
    all_files=sorted((ROOT/'assets/cutouts').glob('*.png'))
    hashes={f.name:hashlib.sha256(f.read_bytes()).hexdigest() for f in all_files}
    report=[]
    for file in all_files:
        number=int(file.name[:2])
        if f'{number:02}' not in SPEC['images']:continue
        baseline=before/file.name
        if not baseline.exists():shutil.copy2(file,baseline)
        old=np.array(Image.open(baseline).convert('RGBA'));image=Image.fromarray(old)
        original=Image.open(ROOT/'assets/images'/(file.stem+'.jpg')).convert('RGB');original.thumbnail((1000,1000),Image.Resampling.LANCZOS)
        assert np.array_equal(np.array(original),old[:,:,:3]),f'Original RGB differs: {file.name}'
        fixed=old.copy();mask=protection_mask(number,image.size);fixed[:,:,3]=np.maximum(old[:,:,3],mask)
        assert np.array_equal(fixed[:,:,:3],old[:,:,:3])
        assert np.all(fixed[:,:,3]>=old[:,:,3])
        changed=(fixed[:,:,3]>old[:,:,3]);ys,xs=np.where(changed)
        Image.fromarray(fixed).save(out/file.name)
        if args.apply:shutil.copy2(out/file.name,file)
        report.append({'number':number,'restored_pixels':int(changed.sum()),'bounds':[int(xs.min()),int(ys.min()),int(xs.max()),int(ys.max())] if xs.size else None,'rgb_unchanged':True})
    if args.apply:
        for file in all_files:
            if file.name[:2] not in SPEC['images']:assert hashlib.sha256(file.read_bytes()).hexdigest()==hashes[file.name]
    (REVIEW/'repair-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
    print(json.dumps(report,ensure_ascii=False))
    print('Applied' if args.apply else 'Prepared previews for',len(report),'reviewed cutouts; original RGB preserved.')

if __name__=='__main__':main()
