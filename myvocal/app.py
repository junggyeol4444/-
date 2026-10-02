from __future__ import annotations
import json
import os
import subprocess
import sys
import tempfile
import threading
import time
import tkinter as tk
from dataclasses import asdict, dataclass, field
from pathlib import Path
from tkinter import filedialog, messagebox, ttk
from .audio import render_demo

BG="#090a0e"; PANEL="#111218"; PANEL2="#181920"; LINE="#292b33"; TEXT="#ececf2"; MUTED="#747681"; ACCENT="#ff6c62"; PURPLE="#a46cff"

@dataclass
class Clip:
    name: str; start: float; length: float

@dataclass
class Track:
    name: str; kind: str; color: str; volume: float=.75; muted: bool=False; solo: bool=False; clips: list[Clip]=field(default_factory=list)

class MyVocalStudio:
    def __init__(self):
        self.root=tk.Tk(); self.root.title("MYVOCAL Studio — 다시, 우리")
        self.root.geometry("1440x900"); self.root.minsize(1050,680); self.root.configure(bg=BG)
        self.root.protocol("WM_DELETE_WINDOW",self.quit); self.root.option_add("*Font",("Segoe UI",9))
        self.playing=False; self.position=72.0; self.bpm=128; self.project_path=None; self.drag=None
        self.tracks=self.default_tracks(); self.track_rows=[]; self.scale=14.0
        self.style(); self.menu(); self.layout(); self.render_timeline(); self.tick()

    def default_tracks(self):
        return [
          Track("Lead Vocal","AI Singing","#ff7168",clips=[Clip("Verse 1",8,16),Clip("Chorus",28,22),Clip("Verse 2",54,18)]),
          Track("Harmony","AI Singing","#b96be5",.58,clips=[Clip("Harmony",30,18),Clip("Final Harmony",72,15)]),
          Track("Drums","Audio","#59c5d5",.68,clips=[Clip("Anime Rock Kit",0,26),Clip("Power Chorus",27,24),Clip("Verse Groove",52,25)]),
          Track("Bass","Instrument","#708fee",.64,clips=[Clip("Bassline",8,18),Clip("Bassline",28,22),Clip("Bassline",53,21)]),
          Track("Electric Guitar","Instrument","#e39a52",.6,clips=[Clip("Clean Arp",8,18),Clip("Power Chords",28,23),Clip("Clean Arp",53,20)]),
          Track("Strings","Instrument","#db6c99",.48,clips=[Clip("Sustain",17,10),Clip("Epic Strings",29,22),Clip("Final Rise",72,16)])]

    def style(self):
        s=ttk.Style(self.root); s.theme_use("clam")
        s.configure("Dark.TButton",background=PANEL2,foreground=TEXT,bordercolor=LINE,padding=7)
        s.map("Dark.TButton",background=[("active","#252730")])
        s.configure("Accent.TButton",background=ACCENT,foreground="white",bordercolor=ACCENT,padding=8)
        s.configure("Dark.Horizontal.TScale",background=PANEL,troughcolor="#262831")
        s.configure("Dark.TCombobox",fieldbackground=PANEL2,background=PANEL2,foreground=TEXT,arrowcolor=TEXT)

    def menu(self):
        bar=tk.Menu(self.root); project=tk.Menu(bar,tearoff=0)
        project.add_command(label="새 프로젝트",accelerator="Ctrl+N",command=self.new_project)
        project.add_command(label="프로젝트 열기…",accelerator="Ctrl+O",command=self.open_project)
        project.add_command(label="저장",accelerator="Ctrl+S",command=self.save_project)
        project.add_command(label="다른 이름으로 저장…",command=lambda:self.save_project(True)); project.add_separator()
        project.add_command(label="종료",command=self.quit); bar.add_cascade(label="파일",menu=project)
        edit=tk.Menu(bar,tearoff=0); edit.add_command(label="트랙 추가",accelerator="Ctrl+T",command=self.add_track_dialog); bar.add_cascade(label="편집",menu=edit)
        helpm=tk.Menu(bar,tearoff=0); helpm.add_command(label="MYVOCAL Studio 정보",command=lambda:messagebox.showinfo("MYVOCAL Studio","MYVOCAL Studio 0.1\nNative desktop prototype"));bar.add_cascade(label="도움말",menu=helpm)
        self.root.config(menu=bar)
        self.root.bind("<Control-n>",lambda e:self.new_project());self.root.bind("<Control-o>",lambda e:self.open_project());self.root.bind("<Control-s>",lambda e:self.save_project());self.root.bind("<Control-t>",lambda e:self.add_track_dialog());self.root.bind("<space>",lambda e:self.toggle_play())

    def frame(self,parent,**kw): return tk.Frame(parent,bg=kw.pop("bg",PANEL),**kw)
    def label(self,parent,text,**kw): return tk.Label(parent,text=text,bg=kw.pop("bg",parent.cget("bg")),fg=kw.pop("fg",TEXT),**kw)
    def button(self,parent,text,command=None,accent=False,width=None): return ttk.Button(parent,text=text,command=command,style="Accent.TButton" if accent else "Dark.TButton",width=width)

    def layout(self):
        top=self.frame(self.root,height=58);top.pack(fill="x");top.pack_propagate(False)
        self.label(top,"M",bg=ACCENT,font=("Segoe UI",14,"bold"),width=3).pack(side="left",padx=(14,8),pady=12)
        self.label(top,"MYVOCAL\nSTUDIO",font=("Segoe UI",10,"bold"),justify="left").pack(side="left")
        sep=self.frame(top,bg=LINE,width=1);sep.pack(side="left",fill="y",padx=18,pady=13)
        self.project_label=self.label(top,"다시, 우리",font=("Segoe UI",10,"bold"));self.project_label.pack(side="left")
        transport=self.frame(top);transport.place(relx=.5,rely=.5,anchor="center")
        self.button(transport,"◀◀",lambda:self.seek(-4),width=4).pack(side="left")
        self.play_btn=self.button(transport,"▶",self.toggle_play,accent=True,width=4);self.play_btn.pack(side="left",padx=5)
        self.button(transport,"■",self.stop,width=4).pack(side="left")
        self.time_label=self.label(transport,"01:12.000",font=("Consolas",13,"bold"));self.time_label.pack(side="left",padx=15)
        self.label(transport,"128.0\nBPM",fg=MUTED,font=("Consolas",8)).pack(side="left",padx=8)
        self.label(transport,"A min\nKEY",fg=MUTED,font=("Consolas",8)).pack(side="left",padx=8)
        self.button(top,"내보내기",self.export_audio).pack(side="right",padx=8,pady=13)
        self.button(top,"✦  RENDER",self.render_project,accent=True).pack(side="right",pady=13)
        body=self.frame(self.root,bg=BG);body.pack(fill="both",expand=True)
        nav=self.frame(body,bg="#0d0e13",width=62);nav.pack(side="left",fill="y");nav.pack_propagate(False)
        for i,(icon,name) in enumerate((("♫","MUSIC"),("◉","VOICE"),("♙","CHARACTER"),("▣","VIDEO"),("▦","LIBRARY"))):
            b=tk.Button(nav,text=f"{icon}\n{name}",bg="#1c1d24" if i==0 else "#0d0e13",fg=TEXT if i==0 else MUTED,activebackground="#24262d",activeforeground=TEXT,border=0,font=("Segoe UI",8),command=lambda n=name:self.switch_workspace(n));b.pack(fill="x",pady=2,ipady=7)
        right=self.frame(body,width=294);right.pack(side="right",fill="y");right.pack_propagate(False);self.build_producer(right)
        center=self.frame(body,bg=BG);center.pack(side="left",fill="both",expand=True);self.build_arranger(center)

    def build_arranger(self,parent):
        toolbar=self.frame(parent,height=45);toolbar.pack(fill="x");toolbar.pack_propagate(False)
        self.workspace=self.label(toolbar,"Music Studio",font=("Segoe UI",10,"bold"));self.workspace.pack(side="left",padx=13)
        for t in ("↖","✂","✎","⌫"): self.button(toolbar,t,width=3).pack(side="left",padx=2,pady=8)
        self.snap=tk.BooleanVar(value=True);tk.Checkbutton(toolbar,text="SNAP",variable=self.snap,bg=PANEL,fg=MUTED,selectcolor=PANEL2,activebackground=PANEL).pack(side="left",padx=10)
        self.button(toolbar,"＋ TRACK",self.add_track_dialog).pack(side="right",padx=8,pady=8)
        sections=self.frame(parent,bg="#101117",height=26);sections.pack(fill="x");sections.pack_propagate(False)
        for text,w,color in (("INTRO",1,"#596078"),("VERSE 1",2,"#546a99"),("PRE-CHORUS",1,"#81647e"),("CHORUS",2,"#a35170"),("VERSE 2",2,"#546a99"),("BRIDGE",1,"#6c578b")):
            f=self.frame(sections,bg="#101117");f.pack(side="left",fill="both",expand=True);self.label(f,text,fg=MUTED,font=("Consolas",7)).pack(anchor="w",padx=6,pady=6);tk.Frame(f,bg=color,height=2).place(relx=0,rely=1,relwidth=1,anchor="sw")
        paned=tk.PanedWindow(parent,orient="vertical",bg=LINE,sashwidth=4,border=0);paned.pack(fill="both",expand=True)
        arrange=self.frame(paned,bg=BG);paned.add(arrange,minsize=260,stretch="always")
        self.track_panel=self.frame(arrange,width=190);self.track_panel.pack(side="left",fill="y");self.track_panel.pack_propagate(False)
        canvas_wrap=self.frame(arrange,bg=BG);canvas_wrap.pack(side="left",fill="both",expand=True)
        self.timeline=tk.Canvas(canvas_wrap,bg="#0d0e13",highlightthickness=0,xscrollincrement=7);self.timeline.pack(fill="both",expand=True)
        xbar=ttk.Scrollbar(canvas_wrap,orient="horizontal",command=self.timeline.xview);xbar.pack(fill="x");self.timeline.configure(xscrollcommand=xbar.set)
        self.timeline.bind("<ButtonPress-1>",self.canvas_press);self.timeline.bind("<B1-Motion>",self.canvas_drag);self.timeline.bind("<ButtonRelease-1>",self.canvas_release)
        editor=self.frame(paned,height=210);paned.add(editor,minsize=150);self.build_editor(editor)

    def build_editor(self,parent):
        tabs=self.frame(parent,height=33);tabs.pack(fill="x");
        content=self.frame(parent,bg="#101116");content.pack(fill="both",expand=True)
        def show(name):
            for w in content.winfo_children():w.destroy()
            if name=="LYRICS":
                left=self.frame(content,bg="#101116",width=140);left.pack(side="left",fill="y",padx=14,pady=14);self.label(left,"VERSE 1",fg="#c57a9a",font=("Consolas",8)).pack(anchor="w");self.label(left,"마디 9–16",font=("Segoe UI",10,"bold")).pack(anchor="w",pady=7)
                text=tk.Text(content,bg="#14151a",fg=TEXT,insertbackground=TEXT,border=0,font=("Malgun Gothic",11),undo=True);text.pack(side="left",fill="both",expand=True,padx=4,pady=10);text.insert("1.0","길었던 계절 끝에 네가 서 있어\n\n잊었던 이름을 다시 불러 봐\n\n멈춰 있던 시간이 흐르기 시작해")
                self.button(content,"✦ AI 다시 쓰기",lambda:self.rewrite_lyrics(text)).pack(side="right",anchor="n",padx=12,pady=12)
            elif name=="PIANO ROLL":
                c=tk.Canvas(content,bg="#111218",highlightthickness=0);c.pack(fill="both",expand=True);self.draw_piano(c)
            elif name=="AUTOMATION":
                c=tk.Canvas(content,bg="#111218",highlightthickness=0);c.pack(fill="both",expand=True);self.root.after(50,lambda:self.draw_automation(c))
            else:
                for i,(title,timing) in enumerate((("도시 야경","00:00–00:12"),("LUNA 등장","00:12–00:28"),("Close-up","00:28–00:45"))):
                    card=self.frame(content,bg=PANEL2,width=150,height=115);card.pack(side="left",padx=10,pady=14);card.pack_propagate(False);self.label(card,f"0{i+1}",fg=ACCENT,font=("Consolas",8)).pack(anchor="w",padx=10,pady=8);self.label(card,title,font=("Segoe UI",9,"bold")).pack(anchor="sw",padx=10,expand=True);self.label(card,timing,fg=MUTED,font=("Consolas",7)).pack(anchor="w",padx=10,pady=7)
        for name in ("LYRICS","PIANO ROLL","AUTOMATION","STORYBOARD"): self.button(tabs,name,lambda n=name:show(n)).pack(side="left",padx=2,pady=3)
        show("LYRICS")

    def build_producer(self,parent):
        self.label(parent,"AI PRODUCER",font=("Consolas",8,"bold")).pack(fill="x",padx=14,pady=16)
        status=self.frame(parent,bg=PANEL2);status.pack(fill="x",padx=10,pady=(0,8));self.label(status,"✦",bg=PURPLE,font=("Segoe UI",13),width=3).pack(side="left",padx=9,pady=10);self.label(status,"AI Producer\n● 프로젝트 분석 완료",bg=PANEL2,font=("Segoe UI",8,"bold"),justify="left").pack(side="left")
        suggest=self.frame(parent,bg="#1a181e");suggest.pack(fill="x",padx=10,pady=4);self.label(suggest,"SUGGESTION",bg="#1a181e",fg="#c381dc",font=("Consolas",7)).pack(anchor="w",padx=10,pady=(10,3));self.label(suggest,"후렴의 에너지를 더 높일 수 있어요.",bg="#1a181e",wraplength=240).pack(anchor="w",padx=10,pady=5)
        acts=self.frame(suggest,bg="#1a181e");acts.pack(fill="x",padx=8,pady=(2,10));self.button(acts,"드럼 강화",lambda:self.ai_apply("후렴 드럼을 강화해줘")).pack(side="left",padx=2);self.button(acts,"화음 추가",lambda:self.ai_apply("후렴에 화음을 추가해줘")).pack(side="left",padx=2)
        self.chat=tk.Text(parent,bg=PANEL,fg="#c9c9d0",insertbackground=TEXT,border=0,state="normal",wrap="word",font=("Malgun Gothic",8),padx=12,pady=10);self.chat.pack(fill="both",expand=True);self.chat.insert("end","✦ 무엇을 바꿔드릴까요? 프로젝트 트랙과 구조를 직접 수정할 수 있어요.\n\n");self.chat.configure(state="disabled")
        prompt=self.frame(parent,bg=PANEL2);prompt.pack(fill="x",padx=10,pady=10);self.ai_entry=tk.Text(prompt,height=3,bg=PANEL2,fg=TEXT,insertbackground=TEXT,border=0,font=("Malgun Gothic",8));self.ai_entry.pack(fill="x",padx=8,pady=7);self.ai_entry.insert("1.0","마지막 후렴만 반키 올려줘");self.button(prompt,"적용  ↑",self.send_ai,accent=True).pack(anchor="e",padx=7,pady=(0,7))
        master=self.frame(parent,bg="#0e0f14");master.pack(fill="x");self.label(master,"MASTER                         -6.2 dB",bg="#0e0f14",font=("Consolas",8)).pack(fill="x",padx=12,pady=8);ttk.Scale(master,from_=0,to=100,value=78,style="Dark.Horizontal.TScale").pack(fill="x",padx=12,pady=(0,12))

    def render_timeline(self):
        for w in self.track_panel.winfo_children():w.destroy()
        self.timeline.delete("all"); row_h=58; width=1400
        self.timeline.create_rectangle(0,0,width,28,fill="#121319",outline=LINE)
        for n in range(0,101,8):
            x=n*self.scale;self.timeline.create_line(x,0,x,28+len(self.tracks)*row_h,fill="#292a31");self.timeline.create_text(x+6,14,text=str(n//2+1),fill=MUTED,font=("Consolas",7),anchor="w")
        self.track_rows=[]
        for idx,t in enumerate(self.tracks):
            row=self.frame(self.track_panel,height=row_h);row.pack(fill="x");row.pack_propagate(False);tk.Frame(row,bg=t.color,width=3).pack(side="left",fill="y")
            self.label(row,"◉" if t.kind=="AI Singing" else "♫",fg=t.color,font=("Segoe UI",12)).pack(side="left",padx=7)
            info=self.frame(row);info.pack(side="left",fill="both",expand=True);self.label(info,t.name,font=("Segoe UI",8,"bold")).pack(anchor="w",pady=(8,0));self.label(info,t.kind.upper(),fg=MUTED,font=("Consolas",6)).pack(anchor="w")
            m=tk.Button(row,text="M",bg=PANEL2,fg=ACCENT if t.muted else MUTED,border=0,width=2,command=lambda i=idx:self.toggle_track(i,"muted"));m.pack(side="left",padx=1)
            s=tk.Button(row,text="S",bg=PANEL2,fg=PURPLE if t.solo else MUTED,border=0,width=2,command=lambda i=idx:self.toggle_track(i,"solo"));s.pack(side="left",padx=2)
            y=28+idx*row_h;self.timeline.create_rectangle(0,y,width,y+row_h,fill="#0f1015" if idx%2 else "#111217",outline="#22232a")
            for ci,c in enumerate(t.clips):
                x=c.start*self.scale;w=max(35,c.length*self.scale);tag=f"clip:{idx}:{ci}";self.timeline.create_rectangle(x,y+7,x+w,y+row_h-7,fill=self.mix(t.color,"#111217",.28),outline=t.color,width=1,tags=(tag,"clip"));self.timeline.create_text(x+7,y+17,text=c.name,fill=t.color,font=("Segoe UI",7,"bold"),anchor="w",tags=(tag,"clip"));
                for px in range(int(x+7),int(x+w-4),7):self.timeline.create_line(px,y+31,px,y+31+(px%13),fill=self.mix(t.color,"#111217",.65),tags=(tag,"clip"))
        self.playhead=self.timeline.create_line(self.position*self.scale,0,self.position*self.scale,28+len(self.tracks)*row_h,fill=ACCENT,width=1,tags="playhead")
        self.timeline.tag_raise("playhead");self.timeline.configure(scrollregion=(0,0,width,28+len(self.tracks)*row_h))

    @staticmethod
    def mix(a,b,p):
        ah=a.lstrip('#');bh=b.lstrip('#');return '#'+''.join(f'{int(int(ah[i:i+2],16)*p+int(bh[i:i+2],16)*(1-p)):02x}' for i in (0,2,4))
    def canvas_press(self,e):
        x=self.timeline.canvasx(e.x);y=self.timeline.canvasy(e.y);items=self.timeline.find_overlapping(x,y,x,y);tags=next((self.timeline.gettags(i) for i in reversed(items) if "clip" in self.timeline.gettags(i)),None)
        if tags:
            ident=next(t for t in tags if t.startswith("clip:"));_,ti,ci=ident.split(':');self.drag=(int(ti),int(ci),x,self.tracks[int(ti)].clips[int(ci)].start)
        else:self.position=max(0,x/self.scale);self.update_playhead()
    def canvas_drag(self,e):
        if not self.drag:return
        ti,ci,start_x,old=self.drag;new=max(0,old+(self.timeline.canvasx(e.x)-start_x)/self.scale);new=round(new/2)*2 if self.snap.get() else new;self.tracks[ti].clips[ci].start=new;self.render_timeline()
    def canvas_release(self,e):self.drag=None
    def toggle_track(self,i,key):setattr(self.tracks[i],key,not getattr(self.tracks[i],key));self.render_timeline()
    def toggle_play(self):self.playing=not self.playing;self.play_btn.config(text="Ⅱ" if self.playing else "▶")
    def stop(self):self.playing=False;self.position=0;self.play_btn.config(text="▶");self.update_playhead()
    def seek(self,amount):self.position=max(0,self.position+amount);self.update_playhead()
    def tick(self):
        if self.playing:self.position=(self.position+.05)%100;self.update_playhead()
        self.root.after(50,self.tick)
    def update_playhead(self):
        self.time_label.config(text=f"{int(self.position//60):02}:{int(self.position%60):02}.{int(self.position%1*1000):03}")
        if hasattr(self,"playhead"):self.timeline.coords(self.playhead,self.position*self.scale,0,self.position*self.scale,28+len(self.tracks)*58)
    def switch_workspace(self,name):self.workspace.config(text=f"{name.title()} Studio");messagebox.showinfo("Workspace",f"{name.title()} Studio 작업 공간으로 전환했습니다.")
    def add_track_dialog(self):
        win=tk.Toplevel(self.root);win.title("트랙 추가");win.configure(bg=PANEL);win.transient(self.root);win.grab_set();win.geometry("390x260");self.label(win,"새 트랙",font=("Segoe UI",16,"bold")).pack(anchor="w",padx=20,pady=16)
        for kind,color in (("AI Singing",ACCENT),("Instrument","#718eed"),("Audio","#59c5d5"),("Video Scene","#db6d99")):
            self.button(win,f"＋  {kind}",lambda k=kind,c=color:(self.tracks.append(Track(f"New {k}",k,c)),self.render_timeline(),win.destroy())).pack(fill="x",padx=20,pady=3)
    def ai_apply(self,prompt):
        self.chat.config(state="normal");self.chat.insert("end",f"나: {prompt}\n");answer="요청을 분석해 프로젝트에 적용했어요."
        if "드럼" in prompt:self.tracks[2].clips.append(Clip("AI Power Drums",76,16));answer="✦ 마지막 후렴에 파워 드럼과 크래시를 추가했어요."
        elif "화음" in prompt:self.tracks[1].clips.append(Clip("AI Harmony",76,15));answer="✦ 마지막 후렴에 3도 위 화음을 추가했어요."
        elif "반키" in prompt:answer="✦ 마지막 후렴을 반키 올리고 전환 구간을 연결했어요."
        self.chat.insert("end",answer+"\n\n");self.chat.see("end");self.chat.config(state="disabled");self.render_timeline()
    def send_ai(self):p=self.ai_entry.get("1.0","end").strip();self.ai_entry.delete("1.0","end");self.ai_apply(p) if p else None
    def rewrite_lyrics(self,text):text.delete("1.0","end");text.insert("1.0","기억의 저편에서 네 목소리가 와\n\n멈춘 계절 사이로 다시 피어난 우리\n\n이제는 놓치지 않을게")
    def draw_piano(self,c):
        for y in range(0,180,18):c.create_line(0,y,1200,y,fill="#25262d");c.create_rectangle(0,y,65,y+18,fill="#ddd" if y//18%2 else "#aaa",outline="#555")
        for i in range(14):x=85+i*53;y=15+(i%6)*19;c.create_rectangle(x,y,x+32+(i%3)*10,y+10,fill="#a96398",outline="#d082ba")
    def draw_automation(self,c):
        w=max(c.winfo_width(),800);pts=[0,120,w*.25,65,w*.5,110,w*.72,35,w,52];c.create_line(*pts,fill=PURPLE,width=2,smooth=True)
        for i in range(0,len(pts),2):c.create_oval(pts[i]-4,pts[i+1]-4,pts[i]+4,pts[i+1]+4,fill=PANEL,outline="#ca8cff",width=2)
    def new_project(self):
        if messagebox.askyesno("새 프로젝트","현재 프로젝트를 닫고 새 프로젝트를 만드시겠습니까?"):self.tracks=self.default_tracks();self.project_path=None;self.position=0;self.render_timeline()
    def save_project(self,save_as=False):
        if save_as or not self.project_path:self.project_path=filedialog.asksaveasfilename(defaultextension=".myvocal",filetypes=[("MYVOCAL Project","*.myvocal")])
        if self.project_path:
            Path(self.project_path).write_text(json.dumps({"bpm":self.bpm,"tracks":[{**asdict(t),"clips":[asdict(c) for c in t.clips]} for t in self.tracks]},ensure_ascii=False,indent=2),encoding="utf-8");self.project_label.config(text=Path(self.project_path).stem)
    def open_project(self):
        p=filedialog.askopenfilename(filetypes=[("MYVOCAL Project","*.myvocal")]);
        if not p:return
        try:
            data=json.loads(Path(p).read_text(encoding="utf-8"));self.bpm=data.get("bpm",128);self.tracks=[Track(**{**t,"clips":[Clip(**c) for c in t["clips"]]}) for t in data["tracks"]];self.project_path=p;self.project_label.config(text=Path(p).stem);self.render_timeline()
        except Exception as e:messagebox.showerror("프로젝트 열기 실패",str(e))
    def export_audio(self):
        p=filedialog.asksaveasfilename(defaultextension=".wav",filetypes=[("WAV Audio","*.wav")]);
        if not p:return
        def work():
            try:render_demo(p,self.bpm);self.root.after(0,lambda:messagebox.showinfo("내보내기 완료",f"오디오를 저장했습니다.\n{p}"))
            except Exception as e:self.root.after(0,lambda:messagebox.showerror("내보내기 실패",str(e)))
        threading.Thread(target=work,daemon=True).start()
    def render_project(self):self.export_audio()
    def quit(self):
        if messagebox.askokcancel("MYVOCAL Studio 종료","프로그램을 종료하시겠습니까?"):self.root.destroy()
    def run(self):self.root.mainloop()
