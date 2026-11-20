"""Original-pixel cutouts. Keep original RGB; only compute a separate alpha mask."""
import sys, json
from pathlib import Path
sys.path.insert(0, str(Path(__file__).parent / 'deps'))
import cv2
import numpy as np
from PIL import Image, ImageDraw

ROOT=Path(__file__).resolve().parents[1]
SOURCE=ROOT/'assets'/'images'
OUT=ROOT/'assets'/'cutouts'; OUT.mkdir(parents=True,exist_ok=True)

def extract(file):
    image=Image.open(file).convert('RGB')
    # Work at display-friendly resolution, preserving the sampled original colors.
    image.thumbnail((1000,1000),Image.Resampling.LANCZOS)
    rgb=np.array(image); h,w=rgb.shape[:2]
    # The halftone backdrop has one dominant ink color and neutral white paper.
    sample=rgb[:int(h*.18),:int(w*.18)].reshape(-1,3)
    quant=(sample//8)*8
    colors,counts=np.unique(quant,axis=0,return_counts=True)
    bg=colors[counts.argmax()].astype(float)+4
    dist=np.linalg.norm(rgb.astype(float)-bg,axis=2)
    white=(rgb.min(axis=2)>235)&(rgb.max(axis=2)-rgb.min(axis=2)<14)
    def color_field(color):
        direction=255-np.array(color,dtype=float)
        t=np.clip(np.sum((rgb.astype(float)-color)*direction,axis=2)/max(1,np.sum(direction**2)),0,1)
        return np.linalg.norm(rgb-(np.array(color)+t[:,:,None]*direction),axis=2)<23
    # Keep pastel star fills even when they share the background hue.
    # Narrow color tolerance, then discard isolated halftone-dot components below.
    allowed=(dist<24)|white
    # The tilted large star behind every full-cast character is foreground decoration.
    # Never remove its solid fill or white rim; only the halftone outer field is background.
    # Flood through the patterned outer field, but never through an outline.
    padded=np.pad(allowed.astype(np.uint8),(1,1),constant_values=1)
    flood=padded.copy(); cv2.floodFill(flood,None,(0,0),2)
    mask=(flood[1:-1,1:-1]!=2).astype(np.uint8)
    # Remove tiny detached halftone remnants, keep lettering and decorative stars.
    n,labels,stats,_=cv2.connectedComponentsWithStats(mask,8)
    keep=np.zeros(n,np.uint8)
    backdrop_tone=color_field(bg)
    for k in range(1,n):
        x,y,cw,ch,area=stats[k]
        if area>max(100,w*h*.0005) or (y<h*.2 and area>45):
            # JPEG antialiasing can leave closed dot rings. They contain only the two
            # backdrop inks; stars, lettering and outlines contain independent colors.
            if area<8000 and y>h*.2 and backdrop_tone[labels==k].mean()>.92: continue
            keep[k]=1
    mask=keep[labels]
    # Faces and central body whites are protected even where original line art has a gap.
    yy,xx=np.ogrid[:h,:w]
    protected=((xx-w*.51)/(w*.19))**2+((yy-h*.55)/(h*.19))**2<1
    mask[protected]=1
    # Recover the narrow white sticker rim that touches the white outer paper.
    expanded=cv2.dilate(mask,np.ones((5,5),np.uint8))
    mask=np.maximum(mask,expanded & white.astype(np.uint8))
    rgba=np.dstack((rgb,mask*255))
    result=Image.fromarray(rgba)
    result.save(OUT/(file.stem+'.png'))
    return result,bg.tolist()

if __name__=='__main__':
    files=sorted(SOURCE.glob('*.jpg')); report=[]
    if not files:
        raise SystemExit('Place your JPEG source artwork in assets/images before running this optional tool.')
    (ROOT/'design').mkdir(parents=True,exist_ok=True)
    sheet=Image.new('RGB',(1500,1020),'#59616c'); d=ImageDraw.Draw(sheet)
    for i,file in enumerate(files):
        result,bg=extract(file); thumb=result.copy();thumb.thumbnail((145,145))
        x=(i%10)*150;y=(i//10)*170
        sheet.paste(thumb,(x+(145-thumb.width)//2,y),thumb)
        d.text((x+5,y+148),file.name[:2],fill='white')
        report.append({'file':file.name,'background':bg})
    sheet.save(ROOT/'design'/'cutouts-contact.jpg')
    (ROOT/'design'/'cutouts-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
    print('Rendered',len(files),'original-pixel cutouts')
