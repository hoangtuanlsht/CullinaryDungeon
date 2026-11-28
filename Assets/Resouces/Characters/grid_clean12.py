import sys, numpy as np
from PIL import Image
from scipy import ndimage
src=sys.argv[1]; out=sys.argv[2]
a=np.array(Image.open(src).convert('RGB')).astype(int)
r,g,b=a[...,0],a[...,1],a[...,2]
bg=(r>170)&(b>170)&(g<120)
fg=~bg
lab,n=ndimage.label(fg); sizes=ndimage.sum(fg,lab,range(1,n+1))
fg=lab==(np.argmax(sizes)+1); fg=ndimage.binary_fill_holes(fg)
# tìm bước lưới + pha: thử pitch 17..22, pha 0..pitch, chọn phương sai trong ô nhỏ nhất
lum=a.mean(-1)
best=None
ys,xs=np.where(fg); y0,y1,x0,x1=ys.min(),ys.max(),xs.min(),xs.max()
for pitch in np.arange(11.0,13.6,0.05):
    for ph in range(0,int(pitch)):
        # dùng biến thiên theo cột: biên màu nên rơi vào ranh giới ô
        cx=np.arange(x0,x1)
        d=np.abs(np.diff(lum[y0:y1,x0:x1+1],axis=1)).sum(0)
        pos=((cx-ph)%pitch)
        on=d[(pos<1)|(pos>pitch-1)].mean(); off=d[(pos>1.5)&(pos<pitch-1.5)].mean()
        sc=on/(off+1e-6)
        if best is None or sc>best[0]: best=(sc,pitch,ph)
sc,pitch,phx=best
d=np.abs(np.diff(lum[y0:y1+1,x0:x1],axis=0)).sum(1); cy=np.arange(y0,y1)
bp=None
for ph in range(int(pitch)):
    pos=((cy-ph)%pitch); s=d[(pos<1)|(pos>pitch-1)].mean()/(d[(pos>1.5)&(pos<pitch-1.5)].mean()+1e-6)
    if bp is None or s>bp[0]: bp=(s,ph)
phy=bp[1]
print('pitch %.1f phase %d %d score %.2f'%(pitch,phx,phy,sc))
H,W=a.shape[:2]
nx=int((W-phx)//pitch); ny=int((H-phy)//pitch)
cells=np.zeros((ny,nx,3),int); mask=np.zeros((ny,nx),bool)
for j in range(ny):
    for i in range(nx):
        ya=int(phy+j*pitch); xa=int(phx+i*pitch)
        ys_=slice(ya+int(pitch*.25),ya+int(pitch*.75)); xs_=slice(xa+int(pitch*.25),xa+int(pitch*.75))
        m=fg[ys_,xs_]
        if m.mean()<0.5: continue
        mask[j,i]=True; cells[j,i]=np.median(a[ys_,xs_][m],axis=0)
# gom màu
pix=cells[mask].astype(np.uint8)
from scipy.cluster.vq import kmeans2
np.random.seed(1)
pal,idx=kmeans2(pix.astype(float),20,minit='++',seed=1)
pal=np.clip(pal,0,255).astype(int)
cells[mask]=pal[idx]
jj,ii=np.where(mask); cells=cells[jj.min():jj.max()+1, ii.min():ii.max()+1]; mask=mask[jj.min():jj.max()+1, ii.min():ii.max()+1]
img=np.zeros(cells.shape[:2]+(4,),np.uint8); img[...,:3]=cells; img[...,3]=mask*255
Image.fromarray(img).save(out); print('size',img.shape[1],'x',img.shape[0])
