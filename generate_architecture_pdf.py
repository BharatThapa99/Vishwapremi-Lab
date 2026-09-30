import os
import sys
from reportlab.lib.pagesizes import letter
from reportlab.lib import colors
from reportlab.pdfgen import canvas
from reportlab.platypus import (
    SimpleDocTemplate, Paragraph, Spacer, Table, TableStyle, KeepTogether, PageBreak, HRFlowable
)
from reportlab.lib.styles import getSampleStyleSheet, ParagraphStyle
from reportlab.lib.enums import TA_CENTER, TA_LEFT, TA_RIGHT, TA_JUSTIFY

class NumberedCanvas(canvas.Canvas):
    def __init__(self, *args, **kwargs):
        super(NumberedCanvas, self).__init__(*args, **kwargs)
        self._saved_page_states = []

    def showPage(self):
        self._saved_page_states.append(dict(self.__dict__))
        self._startPage()

    def save(self):
        num_pages = len(self._saved_page_states)
        for state in self._saved_page_states:
            self.__dict__.update(state)
            self.draw_page_decorations(num_pages)
            super(NumberedCanvas, self).showPage()
        super(NumberedCanvas, self).save()

    def draw_page_decorations(self, page_count):
        # We skip headers on the cover page (page 1)
        if self._pageNumber > 1:
            self.saveState()
            self.setFont("Helvetica-Bold", 8)
            self.setFillColor(colors.HexColor("#184C38"))
            self.drawString(48, 754, "VISHWAPREMI LAB")
            self.setFont("Helvetica", 8)
            self.setFillColor(colors.HexColor("#556B60"))
            self.drawString(138, 754, "— System Architecture, Cryptography & Internal Mechanics")
            
            # Header line
            self.setStrokeColor(colors.HexColor("#D4DED1"))
            self.setLineWidth(0.6)
            self.line(48, 746, 564, 746)

            # Footer line
            self.line(48, 40, 564, 40)
            self.setFont("Helvetica", 8)
            self.setFillColor(colors.HexColor("#556B60"))
            self.drawString(48, 28, "Shree Vishwapremi Secondary School · Arun 5, Yaku, Bhojpur · Designed by Bharat Thapa")
            page_text = f"Page {self._pageNumber} of {page_count}"
            self.drawRightString(564, 28, page_text)
            self.restoreState()

def build_pdf(filename):
    doc = SimpleDocTemplate(
        filename,
        pagesize=letter,
        leftMargin=48,
        rightMargin=48,
        topMargin=46,
        bottomMargin=46
    )

    base_styles = getSampleStyleSheet()
    
    # Custom styles
    styles = {
        'CoverTitle': ParagraphStyle(
            'CoverTitle',
            parent=base_styles['Normal'],
            fontName='Helvetica-Bold',
            fontSize=25,
            leading=30,
            textColor=colors.HexColor('#184C38'),
            alignment=TA_LEFT,
            spaceAfter=8
        ),
        'CoverSubtitle': ParagraphStyle(
            'CoverSubtitle',
            parent=base_styles['Normal'],
            fontName='Helvetica',
            fontSize=12,
            leading=16,
            textColor=colors.HexColor('#2E5A44'),
            alignment=TA_LEFT,
            spaceAfter=15
        ),
        'CoverMeta': ParagraphStyle(
            'CoverMeta',
            parent=base_styles['Normal'],
            fontName='Helvetica',
            fontSize=9.5,
            leading=14,
            textColor=colors.HexColor('#4A5A50'),
            alignment=TA_LEFT
        ),
        'H1': ParagraphStyle(
            'CustomH1',
            parent=base_styles['Normal'],
            fontName='Helvetica-Bold',
            fontSize=14.5,
            leading=18,
            textColor=colors.HexColor('#184C38'),
            spaceBefore=11,
            spaceAfter=4,
            keepWithNext=True
        ),
        'H2': ParagraphStyle(
            'CustomH2',
            parent=base_styles['Normal'],
            fontName='Helvetica-Bold',
            fontSize=11,
            leading=14.5,
            textColor=colors.HexColor('#1F6B47'),
            spaceBefore=8,
            spaceAfter=3,
            keepWithNext=True
        ),
        'Body': ParagraphStyle(
            'CustomBody',
            parent=base_styles['Normal'],
            fontName='Helvetica',
            fontSize=9.3,
            leading=13.2,
            textColor=colors.HexColor('#222B25'),
            spaceAfter=4
        ),
        'BodyBold': ParagraphStyle(
            'CustomBodyBold',
            parent=base_styles['Normal'],
            fontName='Helvetica-Bold',
            fontSize=9.3,
            leading=13.2,
            textColor=colors.HexColor('#184C38'),
            spaceAfter=4
        ),
        'Bullet': ParagraphStyle(
            'CustomBullet',
            parent=base_styles['Normal'],
            fontName='Helvetica',
            fontSize=9,
            leading=12.5,
            textColor=colors.HexColor('#2A362E'),
            leftIndent=14,
            firstLineIndent=-10,
            spaceAfter=2.5
        ),
        'Code': ParagraphStyle(
            'CustomCode',
            parent=base_styles['Normal'],
            fontName='Courier',
            fontSize=8,
            leading=10.5,
            textColor=colors.HexColor('#1C3826')
        ),
        'CalloutText': ParagraphStyle(
            'CalloutText',
            parent=base_styles['Normal'],
            fontName='Helvetica',
            fontSize=9,
            leading=13,
            textColor=colors.HexColor('#1B3B28')
        ),
        'TableHeader': ParagraphStyle(
            'TableHeader',
            parent=base_styles['Normal'],
            fontName='Helvetica-Bold',
            fontSize=8.5,
            leading=11,
            textColor=colors.HexColor('#FFFFFF'),
            alignment=TA_LEFT
        ),
        'TableCell': ParagraphStyle(
            'TableCell',
            parent=base_styles['Normal'],
            fontName='Helvetica',
            fontSize=8.5,
            leading=11,
            textColor=colors.HexColor('#202823')
        ),
        'TableCellBold': ParagraphStyle(
            'TableCellBold',
            parent=base_styles['Normal'],
            fontName='Helvetica-Bold',
            fontSize=8.5,
            leading=11,
            textColor=colors.HexColor('#184C38')
        )
    }

    story = []

    def make_callout(text, title="KEY ARCHITECTURAL PRINCIPLE", color_hex="#184C38", bg_hex="#F0F5F1"):
        p_title = Paragraph(f"<b>{title}</b>", ParagraphStyle('CTitle', parent=styles['CalloutText'], fontName='Helvetica-Bold', textColor=colors.HexColor(color_hex), spaceAfter=2))
        p_body = Paragraph(text, styles['CalloutText'])
        t = Table([[p_title], [p_body]], colWidths=[516])
        t.setStyle(TableStyle([
            ('BACKGROUND', (0,0), (-1,-1), colors.HexColor(bg_hex)),
            ('LEFTPADDING', (0,0), (-1,-1), 12),
            ('RIGHTPADDING', (0,0), (-1,-1), 12),
            ('TOPPADDING', (0,0), (-1,-1), 5),
            ('BOTTOMPADDING', (0,0), (-1,-1), 6),
            ('LINELEFT', (0,0), (0,-1), 3.5, colors.HexColor(color_hex)),
            ('BOX', (0,0), (-1,-1), 0.5, colors.HexColor("#D4DED1")),
        ]))
        return t

    def make_code_box(code_str):
        p = Paragraph(code_str.replace('\n', '<br/>').replace(' ', '&nbsp;'), styles['Code'])
        t = Table([[p]], colWidths=[516])
        t.setStyle(TableStyle([
            ('BACKGROUND', (0,0), (-1,-1), colors.HexColor('#F4F7F4')),
            ('LEFTPADDING', (0,0), (-1,-1), 10),
            ('RIGHTPADDING', (0,0), (-1,-1), 10),
            ('TOPPADDING', (0,0), (-1,-1), 5),
            ('BOTTOMPADDING', (0,0), (-1,-1), 5),
            ('BOX', (0,0), (-1,-1), 0.5, colors.HexColor('#D2DDD0')),
        ]))
        return t

    # ================= COVER / TITLE =================
    # Top decorative banner
    t_banner = Table([['']], colWidths=[516], rowHeights=[6])
    t_banner.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,-1), colors.HexColor('#184C38')),
    ]))
    story.append(t_banner)
    story.append(Spacer(1, 10))

    story.append(Paragraph("VISHWAPREMI LAB MANAGEMENT SYSTEM", styles['CoverTitle']))
    story.append(Paragraph("A Deep-Dive Guide to System Architecture, Cryptography, Networking & Internal Mechanics", styles['CoverSubtitle']))
    
    meta_text = """
    <b>Author & Lead Developer:</b> Bharat Thapa &nbsp;|&nbsp; <b>Institution:</b> Shree Vishwapremi Secondary School, Arun 5, Yaku, Bhojpur<br/>
    <b>Technical Environment:</b> .NET 10 (C# 13), Windows Forms, Windows Defender Firewall API, Win32 DPAPI, GDI+ Engine<br/>
    <b>Current Release:</b> Version 1.4 &nbsp;|&nbsp; <b>Audience:</b> Computer Teachers, System Developers, Network Administrators
    """
    story.append(Paragraph(meta_text, styles['CoverMeta']))
    story.append(Spacer(1, 10))
    story.append(HRFlowable(width="100%", thickness=1, color=colors.HexColor('#D4DED1'), spaceAfter=14))

    # ================= SECTION 1: EXECUTIVE SUMMARY =================
    story.append(Paragraph("1. Executive Summary & Design Philosophy", styles['H1']))
    story.append(Paragraph(
        "Vishwapremi Lab was engineered specifically to solve the harsh realities of operating computer laboratories in public secondary schools in Nepal. "
        "Standard commercial classroom management tools (e.g. NetSupport, LanSchool) rely on complex cloud servers, expensive licensing, external domain controllers, "
        "or high-bandwidth stable internet connections. When internet access is severed or local Wi-Fi router AP isolation interferes, these commercial systems fail.",
        styles['Body']
    ))
    story.append(Paragraph(
        "To achieve 100% resilience, Vishwapremi Lab was designed around five core engineering tenets:",
        styles['Body']
    ))
    story.append(Paragraph("• <b>100% Offline & Zero-Cloud:</b> Every byte of control data, live screen telemetry, and file transfer stays strictly within the local classroom network.", styles['Bullet']))
    story.append(Paragraph("• <b>Zero Pre-Requisite Deployment:</b> Compiles to standalone native x64 self-contained executables. No .NET runtime, Python, IIS, or database server is installed on any computer.", styles['Bullet']))
    story.append(Paragraph("• <b>Self-Healing Dynamic Relocation:</b> Since rural routers randomly change DHCP IP addresses every morning, clients auto-discover and re-anchor to the teacher dynamically within 1.5 seconds.", styles['Bullet']))
    story.append(Paragraph("• <b>Zero-Trust Local Cryptography:</b> Self-sovereign X.509 certificates with SHA-256 certificate pinning eliminate the need for an external Certificate Authority (CA) while preventing spoofing.", styles['Bullet']))
    story.append(Paragraph("• <b>Safety Watchdog Architecture:</b> Failsafe timers ensure student machines never suffer permanent internet lockout if the teacher computer shuts down unexpectedly.", styles['Bullet']))
    
    story.append(Spacer(1, 6))
    story.append(make_callout(
        "Vishwapremi Lab operates entirely peer-to-peer over the school's local switch or Wi-Fi router. "
        "Even if the village fiber optic line or cellular internet is completely dead, the teacher maintains full screen monitoring, classroom control, file distribution, and local policy enforcement.",
        "THE RURAL LAB PARADOX SOLVED"
    ))
    story.append(Spacer(1, 14))

    # ================= SECTION 2: HIGH-LEVEL ARCHITECTURE =================
    story.append(Paragraph("2. High-Level System Architecture", styles['H1']))
    story.append(Paragraph(
        "The suite consists of two distinct native Windows binaries: <b>Vishwapremi.Teacher.exe</b> (the controller workspace) and <b>Vishwapremi.Student.exe</b> (the client terminal agent).",
        styles['Body']
    ))

    # Architecture Table
    arch_data = [
        [Paragraph("Component", styles['TableHeader']), Paragraph("Role & Technology", styles['TableHeader']), Paragraph("Network Ports & Protocols", styles['TableHeader'])],
        [
            Paragraph("<b>Teacher Controller</b>", styles['TableCellBold']),
            Paragraph("Hosts embedded Kestrel HTTPS server, state store, screen coordinator, and auto-update dispatcher.", styles['TableCell']),
            Paragraph("TCP 8766 (Encrypted TLS API)<br/>UDP 8765 (Discovery Broadcast Listener)<br/>UDP 7/9 (Wake-on-LAN Emitter)", styles['TableCell'])
        ],
        [
            Paragraph("<b>Student Terminal</b>", styles['TableCellBold']),
            Paragraph("Runs elevated via Windows Scheduled Task. Executes firewall policies, screen captures, file downloads, and power tasks.", styles['TableCell']),
            Paragraph("Initiates outbound HTTPS to TCP 8766<br/>Broadcasts discovery probe to UDP 8765", styles['TableCell'])
        ],
        [
            Paragraph("<b>Cryptographic Vault</b>", styles['TableCellBold']),
            Paragraph("Windows DPAPI (`CryptProtectData`) binds pairing tokens and teacher certificate fingerprints to local user identity.", styles['TableCell']),
            Paragraph("Local DPAPI RPC (Hardware/OS Keystore)", styles['TableCell'])
        ],
        [
            Paragraph("<b>Kernel Firewall Filter</b>", styles['TableCellBold']),
            Paragraph("Windows Defender Advanced Firewall (`netsh advfirewall`) blocks internet outbound and permits local subnet.", styles['TableCell']),
            Paragraph("WFP (Windows Filtering Platform) Kernel Driver", styles['TableCell'])
        ]
    ]
    t_arch = Table(arch_data, colWidths=[120, 236, 160])
    t_arch.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,0), colors.HexColor('#184C38')),
        ('ALIGN', (0,0), (-1,-1), 'LEFT'),
        ('VALIGN', (0,0), (-1,-1), 'TOP'),
        ('GRID', (0,0), (-1,-1), 0.5, colors.HexColor('#D4DED1')),
        ('ROWBACKGROUNDS', (0,1), (-1,-1), [colors.white, colors.HexColor('#F8FAF8')]),
        ('TOPPADDING', (0,0), (-1,-1), 4),
        ('BOTTOMPADDING', (0,0), (-1,-1), 4),
        ('LEFTPADDING', (0,0), (-1,-1), 6),
        ('RIGHTPADDING', (0,0), (-1,-1), 6),
    ]))
    story.append(t_arch)
    story.append(Spacer(1, 10))

    # ================= SECTION 3: NETWORKING & DYNAMIC DISCOVERY =================
    story.append(Paragraph("3. Network Discovery Engine & Dynamic IP Relocation", styles['H1']))
    story.append(Paragraph(
        "One of the biggest headaches in school computer labs is DHCP IP churn. Every time the Wi-Fi router restarts, the teacher's PC receives a new IP address "
        "(e.g., changing from 192.168.1.15 to 192.168.1.42). In traditional software, every student computer loses connection and must be reconfigured manually. "
        "Vishwapremi Lab solves this with an autonomous <b>dual-channel discovery engine</b> in <code>NetworkSetup.cs</code>.",
        styles['Body']
    ))
    story.append(Paragraph("How Automatic Discovery Operates Behind the Scenes:", styles['H2']))
    story.append(Paragraph("1. <b>UDP Broadcast Probing:</b> When a student computer loses connection for 2 consecutive cycles (8 seconds), it emits a targeted UDP broadcast packet on port <code>8765</code> across all active subnet broadcast addresses.", styles['Bullet']))
    story.append(Paragraph("2. <b>Teacher Challenge Verification:</b> The Teacher background thread listens on UDP port 8765. Upon receiving a discovery probe matching an active enrollment, it replies with its current IP address and port.", styles['Bullet']))
    story.append(Paragraph("3. <b>Cryptographic Certificate Verification:</b> Before accepting the new address, the student initiates a TLS handshake with the responding IP and checks the server's public key against the SHA-256 fingerprint stored in its pairing record.", styles['Bullet']))
    story.append(Paragraph("4. <b>Dynamic Vault Re-Anchor:</b> Once authenticated, the student updates its encrypted <code>connection.bin</code> configuration and resumes polling instantly without showing any error to the user.", styles['Bullet']))

    story.append(Spacer(1, 4))
    story.append(make_code_box(
"""// NetworkSetup.cs: Broadcast Discovery Snippet
using var client = new UdpClient();
client.EnableBroadcast = true;
var probe = Encoding.UTF8.GetBytes("VISHWAPREMI-FIND:" + pairing.Id);
foreach (var sub in ActiveSubnetBroadcasts()) {
    await client.SendAsync(probe, probe.Length, new IPEndPoint(sub, 8765));
}"""
    ))
    story.append(Spacer(1, 10))

    # ================= SECTION 4: SECURITY & CRYPTOGRAPHY =================
    story.append(Paragraph("4. Cryptographic Security & Zero-Trust Architecture", styles['H1']))
    story.append(Paragraph(
        "Because school networks are open environments where tech-savvy students might attempt to inspect traffic, spoof teacher commands, or tamper with grades, "
        "Vishwapremi Lab implements enterprise-grade cryptographic guarantees without requiring any external internet connectivity.",
        styles['Body']
    ))
    
    sec_data = [
        [Paragraph("Security Layer", styles['TableHeader']), Paragraph("Cryptographic Primitive", styles['TableHeader']), Paragraph("Protection Provided", styles['TableHeader'])],
        [
            Paragraph("<b>TLS Transport</b>", styles['TableCellBold']),
            Paragraph("ECDSA P-256 / RSA-2048 with TLS 1.3 encryption", styles['TableCell']),
            Paragraph("Guarantees confidentiality and integrity across the local LAN. Wi-Fi packet sniffers cannot read passwords, screens, or commands.", styles['TableCell'])
        ],
        [
            Paragraph("<b>Certificate Pinning</b>", styles['TableCellBold']),
            Paragraph("SHA-256 Raw Public Key Pinning (64-char hex hash)", styles['TableCell']),
            Paragraph("Eliminates Man-In-The-Middle (MITM) attacks. Rogue laptops pretending to be the teacher are mathematically rejected by clients.", styles['TableCell'])
        ],
        [
            Paragraph("<b>Token Exchange</b>", styles['TableCellBold']),
            Paragraph("Single-Use Nonce & Cryptographic Bearer Tokens", styles['TableCell']),
            Paragraph("Pairing files (.vplab) expire after 24 hours and can only be enrolled once. Token interception replay attacks are impossible.", styles['TableCell'])
        ],
        [
            Paragraph("<b>Credential Vault</b>", styles['TableCellBold']),
            Paragraph("Windows DPAPI (`CryptProtectData` User Scope)", styles['TableCell']),
            Paragraph("Stored pairing tokens on student PCs cannot be copied to a USB drive or used by other Windows user accounts.", styles['TableCell'])
        ]
    ]
    t_sec = Table(sec_data, colWidths=[110, 186, 220])
    t_sec.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,0), colors.HexColor('#184C38')),
        ('ALIGN', (0,0), (-1,-1), 'LEFT'),
        ('VALIGN', (0,0), (-1,-1), 'TOP'),
        ('GRID', (0,0), (-1,-1), 0.5, colors.HexColor('#D4DED1')),
        ('ROWBACKGROUNDS', (0,1), (-1,-1), [colors.white, colors.HexColor('#F8FAF8')]),
        ('TOPPADDING', (0,0), (-1,-1), 4),
        ('BOTTOMPADDING', (0,0), (-1,-1), 4),
        ('LEFTPADDING', (0,0), (-1,-1), 5),
        ('RIGHTPADDING', (0,0), (-1,-1), 5),
    ]))
    story.append(t_sec)
    story.append(Spacer(1, 10))

    # ================= SECTION 5: HTTP CONTROLLER & POLLING =================
    story.append(Paragraph("5. Embedded Controller & Polling State Machine", styles['H1']))
    story.append(Paragraph(
        "Inside <code>Controller.cs</code>, the Teacher application spins up an embedded Kestrel HTTPS server binding to port <code>8766</code>. "
        "The interaction model between Teacher and Student operates on an inverted request-reply <b>Heartbeat Polling State Machine</b>.",
        styles['Body']
    ))
    story.append(Paragraph("Why Polling Instead of Direct Push?", styles['H2']))
    story.append(Paragraph(
        "In typical Wi-Fi routers used in schools, <b>Client Isolation / AP Isolation</b> or Windows Defender inbound firewalls frequently block the teacher PC from opening inbound TCP connections to student laptops. "
        "However, student laptops can <i>always</i> initiate outbound connections to the teacher! "
        "Therefore, students poll the teacher periodically. When the teacher queues an action (such as Lock, Share File, or Block Internet), it is delivered in the immediate next poll response.",
        styles['Body']
    ))

    story.append(Paragraph("Endpoints Hosted on the Teacher Controller:", styles['H2']))
    story.append(Paragraph("• <code>POST /enroll</code>: Validates the single-use pairing token from <code>.vplab</code> and issues a cryptographically secure 256-bit bearer token.", styles['Bullet']))
    story.append(Paragraph("• <code>POST /poll</code>: Receives student status (username, machine name, MAC address) and delivers up to 30 queued commands. Dynamic intervals adjust from 4000ms down to 1500ms during active screen monitoring.", styles['Bullet']))
    story.append(Paragraph("• <code>POST /screen</code>: Receives binary JPEG screen capture frames from the student when monitoring is enabled.", styles['Bullet']))
    story.append(Paragraph("• <code>GET /file/{id}</code>: High-speed streaming file endpoint for classroom file sharing and remote silent software auto-updating.", styles['Bullet']))

    story.append(Spacer(1, 4))
    story.append(make_callout(
        "To prevent duplicate action execution during network packet loss, every command carries a unique GUID. "
        "The student maintains an encrypted replay journal (<code>actions.json</code>). Even if an action is redelivered due to a Wi-Fi drop, the student checks the journal and suppresses re-execution.",
        "IDEMPOTENT COMMAND EXECUTION GUARANTEE"
    ))
    story.append(Spacer(1, 10))

    # ================= SECTION 6: INTERNET KILL SWITCH & FILTERING =================
    story.append(Paragraph("6. Internet Kill Switch, DNS Whitelisting & Hosts Blacklisting", styles['H1']))
    story.append(Paragraph(
        "When students are asked to type in Word or practice QBASIC, they frequently browse YouTube, TikTok, or social media over the school Wi-Fi. "
        "Vishwapremi Lab gives teachers fine-grained network control using a three-tier enforcement engine:",
        styles['Body']
    ))
    
    story.append(Paragraph("Tier 1: Total Internet Kill Switch (`Block Internet`)", styles['H2']))
    story.append(Paragraph(
        "When 'Block Internet' is triggered, the student client executes <code>NetworkSetup.BlockInternet()</code> which instructs the Windows Filtering Platform via <code>netsh advfirewall</code>:",
        styles['Body']
    ))
    story.append(Paragraph("1. Sets the default outbound firewall policy to <b>BLOCK</b> across all profiles (Domain, Private, Public).", styles['Bullet']))
    story.append(Paragraph("2. Injects an allow rule for <b>Loopback</b> (127.0.0.1) so Windows local IPC functions smoothly.", styles['Bullet']))
    story.append(Paragraph("3. Injects an allow rule strictly for the <b>Teacher IP on TCP 8766</b> so classroom commands continue flowing.", styles['Bullet']))
    story.append(Paragraph("4. Injects an allow rule for <b>Local Subnet Broadcasts & DHCP</b> (UDP 67/68) so network connectivity remains stable.", styles['Bullet']))

    story.append(Paragraph("Tier 2: Website Blacklisting (Hosts File Poisoning)", styles['H2']))
    story.append(Paragraph(
        "For blacklisting specific sites (e.g., <code>youtube.com</code>, <code>facebook.com</code>), the app manipulates <code>C:\\Windows\\System32\\drivers\\etc\\hosts</code>. "
        "It injects loopback redirects (<code>127.0.0.1 domain.com</code> and <code>127.0.0.1 www.domain.com</code>) inside marked tags and executes <code>ipconfig /flushdns</code>. "
        "Browsers immediately fail to load the blacklisted domains while all educational sites remain accessible.",
        styles['Body']
    ))

    story.append(Paragraph("Tier 3: Website Whitelisting (Exclusive Domain Allow)", styles['H2']))
    story.append(Paragraph(
        "When a teacher wants students to access <i>only</i> specific educational platforms (e.g. Wikipedia or an exam site), Whitelisting combines both mechanisms: "
        "all general outbound traffic is blocked, outbound DNS (UDP 53) is allowed, and targeted allow rules are dynamically created for the resolved IPv4 addresses of the whitelisted domains on TCP ports 80 and 443.",
        styles['Body']
    ))

    story.append(Paragraph("The 10-Minute Safety Watchdog Failsafe", styles['H2']))
    story.append(Paragraph(
        "<b>What if the teacher's laptop battery dies while students are blocked?</b> "
        "The student daemon includes a watchdog counter in <code>Poll()</code>: if it fails to reach the teacher for 150 consecutive cycles (~10 minutes), "
        "it automatically triggers <code>NetworkSetup.AllowInternet()</code> and restores the original hosts file. Students are never permanently locked out of the internet!",
        styles['Body']
    ))
    story.append(Spacer(1, 10))

    # ================= SECTION 7: SCREEN MONITORING =================
    story.append(Paragraph("7. Real-Time Live Screen Monitoring Pipeline", styles['H1']))
    story.append(Paragraph(
        "Screen monitoring allows the teacher to see all 30 student displays in real time. "
        "Streaming raw video or full-resolution bitmaps would instantly crash a school's 100Mbps Wi-Fi router. "
        "Vishwapremi Lab uses an adaptive pipeline engineered for high visual clarity and near-zero network footprint.",
        styles['Body']
    ))

    story.append(Paragraph("Capture & Compression Pipeline:", styles['H2']))
    story.append(Paragraph("1. <b>GDI+ Hardware Blit:</b> Uses <code>Graphics.CopyFromScreen</code> to take an instant snapshot of the primary desktop buffer.", styles['Bullet']))
    story.append(Paragraph("2. <b>Bilinear Downsampling:</b> In Grid Mode, frames are downsampled to 360px wide (proportional height) using GDI+ Bilinear interpolation.", styles['Bullet']))
    story.append(Paragraph("3. <b>Adaptive JPEG Encoding:</b> Compressed at 45% quality in thumbnail mode (~8KB per frame) and 65% quality in Full View mode (1280px, ~45KB per frame).", styles['Bullet']))
    story.append(Paragraph("4. <b>Dynamic Bandwidth Throttling:</b> Poll intervals automatically throttle: 4.0s when monitor is closed, 3.0s in thumbnail grid, and 1.5s in Full-Screen viewer.", styles['Bullet']))
    story.append(Paragraph("5. <b>Zero-Overhead Idle State:</b> When the Teacher closes the Screen Monitor window, <code>ScreenMode</code> drops to <code>0</code>. Students immediately stop capturing frames, reducing network load and CPU usage to 0.0%.", styles['Bullet']))
    story.append(Spacer(1, 10))

    # ================= SECTION 8: FILE TRANSFER & AUTO-UPDATE =================
    story.append(Paragraph("8. File Distribution & Remote Silent Auto-Updating", styles['H1']))
    story.append(Paragraph(
        "Teachers frequently need to distribute worksheets, PDF notes, or programming problem sets without walking around the lab with a USB flash drive. "
        "Similarly, upgrading 30 computers with a new software build previously took hours.",
        styles['Body']
    ))

    story.append(Paragraph("High-Throughput Chunked Streaming:", styles['H2']))
    story.append(Paragraph(
        "File transfers use a dedicated 15-minute HTTP client (<code>dlClient</code>) configured with <code>HttpCompletionOption.ResponseHeadersRead</code>. "
        "Data streams directly from disk into an 80KB buffer, avoiding memory bloat and preventing timeouts on multi-megabyte files.",
        styles['Body']
    ))

    story.append(Paragraph("Remote Silent Auto-Updating Mechanics:", styles['H2']))
    story.append(Paragraph("1. Teacher clicks 'Update Student Apps'. The controller registers the latest <code>Vishwapremi-Student-Setup.exe</code> and computes its SHA-256 hash.", styles['Bullet']))
    story.append(Paragraph("2. Student receives the <code>update-app</code> command, downloads the installer to <code>%TEMP%\\VishwapremiUpdate\\</code>, and verifies the SHA-256 checksum.", styles['Bullet']))
    story.append(Paragraph("3. Student dispatches a detached helper script (<code>run_update.bat</code>) and shuts itself down cleanly.", styles['Bullet']))
    story.append(Paragraph("4. The script executes the Inno Setup installer silently: <code>/VERYSILENT /SUPPRESSMSGBOXES /NORESTART /CLOSEAPPLICATIONS</code> and relaunches the upgraded binary with elevated permissions.", styles['Bullet']))
    story.append(Spacer(1, 10))

    # ================= SECTION 9: POWER MANAGEMENT & WOL =================
    story.append(Paragraph("9. Classroom Power Management & Wake-on-LAN", styles['H1']))
    story.append(Paragraph(
        "At the beginning and end of computer periods, turning on or shutting down 30 individual desktop towers wastes valuable class time. "
        "Vishwapremi Lab integrates complete remote power management into both the Teacher Dashboard and Screen Monitor:",
        styles['Body']
    ))
    story.append(Paragraph("• <b>Shutdown with Warning:</b> Dispatches <code>shutdown.exe /s /t 15 /f</code> with a student balloon notice so students can save unsaved files.", styles['Bullet']))
    story.append(Paragraph("• <b>Immediate Shutdown & Reboot:</b> Reboots in 5 seconds (<code>shutdown.exe /r /t 5 /f</code>) or shuts down immediately (0s).", styles['Bullet']))
    story.append(Paragraph("• <b>User Logoff:</b> Signs out student Windows profiles (<code>shutdown.exe /l</code>) ready for the next incoming class.", styles['Bullet']))
    story.append(Paragraph("• <b>Low-Power Sleep:</b> Invokes <code>Application.SetSuspendState(PowerState.Suspend, force: true, disableWakeEvent: false)</code> to save school electricity between sessions.", styles['Bullet']))
    story.append(Paragraph("• <b>Wake-on-LAN (WoL):</b> Sends AMD Magic Packets over UDP broadcast (ports 9 & 7) to wake up sleeping or powered-off desktop computers across the lab. MAC addresses are automatically harvested during regular polling.", styles['Bullet']))

    story.append(Spacer(1, 4))
    story.append(make_code_box(
"""// AMD Magic Packet Assembly (NetworkSetup.cs)
byte[] macBytes = Convert.FromHexString(macClean);
byte[] packet = new byte[102];
Array.Fill(packet, (byte)0xFF, 0, 6); // 6 synchronization bytes
for (int i = 0; i < 16; i++) {
    Buffer.BlockCopy(macBytes, 0, packet, 6 + i * 6, 6); // Target MAC x 16
}"""
    ))
    story.append(Spacer(1, 10))

    # ================= SECTION 10: PRIVILEGE ELEVATION =================
    story.append(Paragraph("10. Windows Privilege Elevation & Scheduled Task Persistence", styles['H1']))
    story.append(Paragraph(
        "Modifying Windows Firewall policies (<code>netsh</code>) and the system hosts file requires local Administrator elevation. "
        "However, student desktop accounts in school labs are standard non-administrator users, and prompting a UAC popup on student screens every morning would allow students to bypass controls.",
        styles['Body']
    ))
    story.append(Paragraph("The Scheduled Task Elevation Pattern:", styles['H2']))
    story.append(Paragraph(
        "During setup (or via the 'Enable Admin Control' button), the application registers a Windows Scheduled Task with highest privileges:",
        styles['Body']
    ))
    story.append(make_code_box(
"""schtasks.exe /create /tn "VishwapremiStudent" \\
   /tr "\"C:\\Users\\Student\\AppData\\Local\\Programs\\Vishwapremi Student\\Vishwapremi.Student.exe\" --startup" \\
   /sc onlogon /rl HIGHEST /f"""
    ))
    story.append(Spacer(1, 4))
    story.append(Paragraph(
        "When the student logs into Windows, Task Scheduler launches the Student agent with full administrative tokens <b>without displaying any UAC consent prompt</b>. "
        "This gives the client full authority to apply firewall rules, clear hosts files, and execute power commands seamlessly.",
        styles['Body']
    ))
    story.append(Spacer(1, 10))

    # ================= SECTION 11: SOURCE CODE ARCHITECTURE =================
    story.append(Paragraph("11. Source Code Map & Engineering Reference", styles['H1']))
    story.append(Paragraph(
        "The codebase is cleanly separated into shared libraries, teacher tools, and student execution agents:",
        styles['Body']
    ))

    code_map = [
        [Paragraph("Source File", styles['TableHeader']), Paragraph("Lines / Scope", styles['TableHeader']), Paragraph("Core Responsibilities & Implementation Details", styles['TableHeader'])],
        [
            Paragraph("<b>Shared/Core.cs</b>", styles['TableCellBold']),
            Paragraph("Models, Records, Validation", styles['TableCell']),
            Paragraph("Defines data structures (`Device`, `Command`, `Pairing`, `Ack`), command input validation, and domain parsing.", styles['TableCell'])
        ],
        [
            Paragraph("<b>Shared/Desktop.cs</b>", styles['TableCellBold']),
            Paragraph("UI Design System, DPAPI Vault", styles['TableCell']),
            Paragraph("Defines color palette, interactive button hover styles, dialog helpers, DPAPI encryption wrapper, and error handlers.", styles['TableCell'])
        ],
        [
            Paragraph("<b>Shared/NetworkSetup.cs</b>", styles['TableCellBold']),
            Paragraph("Networking, Firewall, WoL", styles['TableCell']),
            Paragraph("Netsh firewall rule injection, hosts file poisoning, Wake-on-LAN packet synthesis, UDP discovery beaconing, MAC harvesting.", styles['TableCell'])
        ],
        [
            Paragraph("<b>Teacher/TeacherForm.cs</b>", styles['TableCellBold']),
            Paragraph("Teacher Main Dashboard", styles['TableCell']),
            Paragraph("Room management, instant computer search bar, action bar, DataGridView status chips, pairing generation, power dialog.", styles['TableCell'])
        ],
        [
            Paragraph("<b>Teacher/Controller.cs</b>", styles['TableCellBold']),
            Paragraph("Kestrel HTTPS Server", styles['TableCell']),
            Paragraph("Hosts endpoints (`/poll`, `/enroll`, `/screen`, `/file`), TLS certificate generation, token verification, thread-safe queuing.", styles['TableCell'])
        ],
        [
            Paragraph("<b>Teacher/ScreenMonitorForm.cs</b>", styles['TableCellBold']),
            Paragraph("Classroom Live Screens", styles['TableCell']),
            Paragraph("Card grid view, real-time PC search filter, mini action buttons, full-screen live 1080p viewer with screenshot capture.", styles['TableCell'])
        ],
        [
            Paragraph("<b>Student/StudentForm.cs</b>", styles['TableCellBold']),
            Paragraph("Student Terminal Daemon", styles['TableCell']),
            Paragraph("Card-based UI, polling loop, GDI+ screen capture, command execution dispatcher, safety watchdog, system tray minimization.", styles['TableCell'])
        ],
        [
            Paragraph("<b>Tests/Program.cs</b>", styles['TableCellBold']),
            Paragraph("64 Integration Tests", styles['TableCell']),
            Paragraph("Validates encryption, command queuing, payload validation, TLS enrollment, network discovery, and crash survival.", styles['TableCell'])
        ],
        [
            Paragraph("<b>setup.iss & Build.ps1</b>", styles['TableCellBold']),
            Paragraph("Inno Setup Packaging", styles['TableCell']),
            Paragraph("Compiles self-contained release binaries, registers scheduled tasks, creates shortcuts, and generates installers.", styles['TableCell'])
        ]
    ]
    t_map = Table(code_map, colWidths=[140, 95, 281])
    t_map.setStyle(TableStyle([
        ('BACKGROUND', (0,0), (-1,0), colors.HexColor('#184C38')),
        ('ALIGN', (0,0), (-1,-1), 'LEFT'),
        ('VALIGN', (0,0), (-1,-1), 'TOP'),
        ('GRID', (0,0), (-1,-1), 0.5, colors.HexColor('#D4DED1')),
        ('ROWBACKGROUNDS', (0,1), (-1,-1), [colors.white, colors.HexColor('#F8FAF8')]),
        ('TOPPADDING', (0,0), (-1,-1), 4),
        ('BOTTOMPADDING', (0,0), (-1,-1), 4),
        ('LEFTPADDING', (0,0), (-1,-1), 5),
        ('RIGHTPADDING', (0,0), (-1,-1), 5),
    ]))
    story.append(t_map)
    story.append(Spacer(1, 10))

    # Concluding Note
    story.append(Paragraph("12. Conclusion & Educational Legacy", styles['H1']))
    story.append(Paragraph(
        "Vishwapremi Lab is proof that world-class, resilient computer lab management software does not require expensive enterprise licenses, "
        "cloud subscriptions, or high-speed internet. By leveraging modern C# and .NET 10, self-sovereign pinned TLS, native Windows firewall kernel drivers, "
        "and resilient local peer discovery, Shree Vishwapremi Secondary School has a management system tailored specifically for educational independence in Nepal.",
        styles['Body']
    ))
    story.append(Spacer(1, 10))
    story.append(Paragraph("<i>Crafted with dedication for Shree Vishwapremi Secondary School, Arun 5, Yaku, Bhojpur.<br/>Designed & Engineered by Bharat Thapa.</i>", styles['CoverMeta']))

    doc.build(story, canvasmaker=NumberedCanvas)
    print(f"PDF successfully generated at: {filename} ({os.path.getsize(filename)} bytes)")

if __name__ == '__main__':
    out_dir = r"c:\desktop project\Vishwapremi-Lab\Release"
    os.makedirs(out_dir, exist_ok=True)
    out_file = os.path.join(out_dir, "Vishwapremi-Lab-System-Architecture-Guide.pdf")
    build_pdf(out_file)
    # Also place a copy in project root for immediate access
    root_file = r"c:\desktop project\Vishwapremi-Lab\Vishwapremi-Lab-System-Architecture-Guide.pdf"
    import shutil
    shutil.copyfile(out_file, root_file)
    print(f"Copied to root: {root_file}")
