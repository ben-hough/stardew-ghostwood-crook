"""Regenerates the mod's 16x16 pixel art (assets/crook.png, assets/phantom_post.png).
Run from the project folder: python tools/generate_art.py (requires Pillow)."""
from PIL import Image
S=16
core = {
 1:[10,11,12,13],
 2:[9,10,11,12,13,14],
 3:[9,10,13,14],
 4:[9,10,14],
 5:[8,9,14],
 6:[7,8],
 7:[6,7],
 8:[5,6],
 9:[4,5],
 10:[3,4],
 11:[2,3],
 12:[1,2],
 13:[1],
}
m=[[False]*S for _ in range(S)]
for y,xs in core.items():
    for x in xs: m[y][x]=True
OUT=(48,58,80,255); BASE=(206,219,214,255); HI=(244,252,249,255); SH=(172,190,194,255)
GLOW=(150,240,228,170); GLOW2=(150,240,228,90)
img=Image.new("RGBA",(S,S),(0,0,0,0)); px=img.load()
def inm(x,y): return 0<=x<S and 0<=y<S and m[y][x]
for y in range(S):
  for x in range(S):
    if m[y][x]:
      if not inm(x,y-1) and not inm(x+1,y): px[x,y]=BASE
      if not inm(x-1,y) or not inm(x,y-1): px[x,y]=HI
      elif not inm(x+1,y) or not inm(x,y+1): px[x,y]=SH
      else: px[x,y]=BASE
for y in range(S):
  for x in range(S):
    if not m[y][x] and any(inm(x+dx,y+dy) for dx,dy in ((1,0),(-1,0),(0,1),(0,-1),(1,1),(-1,-1),(1,-1),(-1,1))):
      px[x,y]=OUT
# grain
for x,y in ((5,8),(3,10),(11,2)):
  px[x,y]=SH
# ghostly wisps
for x,y,c in ((12,4,GLOW),(12,7,GLOW2),(14,9,GLOW),(6,2,GLOW2),(9,11,GLOW),(11,13,GLOW2),(2,5,GLOW2)):
  if px[x,y][3]==0 or (x,y)==(12,4): px[x,y]=c
img.save("assets/crook.png")

S = 16
# phantom fence post (drawn at 4x in game, alpha applied by code)
post = Image.new("RGBA", (S, S), (0, 0, 0, 0))
p = post.load()
PO = (120, 200, 210, 255); PB = (205, 245, 245, 255); PH = (255, 255, 255, 255); PS = (160, 215, 225, 255)
for y in range(3, 15):
    for x in range(5, 11):
        edge = x in (5, 10) or y == 3 or y == 14
        p[x, y] = PO if edge else (PH if x == 6 else (PS if x == 9 else PB))
for x in (6, 7, 8, 9):
    p[x, 2] = PO
p[7, 1] = PO; p[8, 1] = PO
# wisp tail at bottom
for (x, y, a) in ((4, 15, 120), (11, 15, 120), (7, 15, 200), (8, 15, 160)):
    p[x, y] = (180, 240, 240, a)
post.save("assets/phantom_post.png")
