import numpy as np, os, json
from PIL import Image
from scipy import ndimage
from scipy.cluster.vq import kmeans2
U='/mnt/user-data/uploads/'
files=[('Orc_Chief','f0667b80'),('Orc_Villager','cc943800'),('Orc_Guard','a3b782f3'),
 ('Orc_SoupCook','67e5d5f5'),('Orc_Blacksmith','1bb78daf'),('Orc_Elder','bb9bf3c1'),
 ('Orc_Miner','5f88f384'),('Orc_Kid','58b93d6c'),('Orc_Drunk','bf293860'),('Orc_WaterCarrier','8e5788db')]
PITCH=12.3
OUTC=np.array([10,7,22,255],np.uint8)
def convert(path):
    a=np.array(Image.open(path).convert('RGB')).astype(int)
    r,g,b=a[...,0],a[...,1],a[...,2]
    fg=~((r>170)&(b>170)&(g<120))
    lab,n=ndimage.label(fg); sizes=ndimage.sum(fg,lab,range(1,n+1))
    keep=[i+1 for i,s in enumerate(sizes) if s>2000]
    fg=np.isin(lab,keep)
    lum=a.mean(-1); ys,xs=np.where(fg); y0,y1,x0,x1=ys.min(),ys.max(),xs.min(),xs.max()
    dx=np.abs(np.diff(lum[y0:y1,x0:x1+1],axis=1)).sum(0); cx=np.arange(x0,x1)
    dy=np.abs(np.diff(lum[y0:y1+1,x0:x1],axis=0)).sum(1); cy=np.arange(y0,y1)
    def score(d,c,p,ph):
        pos=(c-ph)%p; return d[(pos<1)|(pos>p-1)].mean()/(d[(pos>1.5)&(pos<p-1.5)].mean()+1e-6)
    best=max(((score(dx,cx,p,ph)+score(dy,cy,p,ph2),p,ph,ph2) for p in [PITCH]
              for ph in range(int(p)) for ph2 in [0]),key=lambda t:t[0])
    p=best[1]; phx=best[2]
    phy=max(range(int(p)),key=lambda ph:score(dy,cy,p,ph))
    H,W=a.shape[:2]; nx=int((W-phx)//p); ny=int((H-phy)//p)
    cells=np.zeros((ny,nx,3),int); mask=np.zeros((ny,nx),bool)
    for j in range(ny):
        for i in range(nx):
            ya=int(phy+j*p); xa=int(phx+i*p)
            sy=slice(ya+int(p*.25),ya+int(p*.75)); sx=slice(xa+int(p*.25),xa+int(p*.75))
            m=fg[sy,sx]
            if m.size==0 or m.mean()<0.5: continue
            med=np.median(a[sy,sx][m],axis=0)
            if med[0]-med[1]>60 and med[2]-med[1]>60: continue
            mask[j,i]=True; cells[j,i]=med
    pix=cells[mask].astype(float)
    pal,idx=kmeans2(pix,22,minit='++',seed=3); pal=np.clip(pal,0,255).astype(int)
    cells[mask]=pal[idx]
    jj,ii=np.where(mask); cells=cells[jj.min():jj.max()+1,ii.min():ii.max()+1]; mask=mask[jj.min():jj.max()+1,ii.min():ii.max()+1]
    img=np.zeros(cells.shape[:2]+(4,),np.uint8); img[...,:3]=cells; img[...,3]=mask*255
    dark=mask&(cells.sum(-1)<60); img[dark]=OUTC
    return img,p
res={}
for name,f in files:
    path=[U+x for x in os.listdir(U) if x.startswith(f)][0]
    img,p=convert(path); res[name]=img
    Image.fromarray(img).save('batch/%s_Single.png'%name)
    print(name,'pitch %.2f'%p,'size',img.shape[1],'x',img.shape[0])
