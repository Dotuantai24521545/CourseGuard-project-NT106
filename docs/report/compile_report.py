import os
import html

# Define paths
report_dir = r"c:\Documentss\LtrinhMangCanBan\NT106_QuanLyKhoaHoc_ThiOnline\docs\report"
output_file = r"c:\Documentss\LtrinhMangCanBan\NT106_QuanLyKhoaHoc_ThiOnline\report.html"

# Markdown files to combine in logical order
files_in_order = [
    ("gioi-thieu", "1. Giới thiệu đề tài", "GIOI_THIEU_DE_TAI_COURSEGUARD.md"),
    ("danh-sach-chuc-nang", "2. Danh sách chức năng", "DANH_SACH_CHUC_NANG_COURSEGUARD.md"),
    ("phan-tich-usecase", "3. Phân tích Usecase", "PHAN_TICH_USECASE_COURSEGUARD.md"),
    ("phan-tich-kien-truc", "4. Phân tích Kiến trúc", "PHAN_TICH_KIEN_TRUC_COURSEGUARD.md"),
    ("phan-tich-giao-tiep", "5. Giao tiếp Hệ thống (Mạng TCP & DB)", "PHAN_TICH_GIAO_TIEP_HE_THONG_COURSEGUARD.md"),
    ("activity-flow", "6. Luồng hoạt động (Activity Flow)", "ACTIVITY_FLOW_UPLOAD_SCAN_RESULT_COURSEGUARD.md"),
    ("cai-dat-chuong-trinh", "7. Hướng dẫn cài đặt", "CAI_DAT_CHUONG_TRINH_COURSEGUARD.md"),
    ("ket-qua-dat-duoc", "8. Kết quả & Hạn chế", "KET_QUA_DAT_DUOC_VA_HAN_CHE_COURSEGUARD.md"),
    ("ket-luan", "9. Kết luận", "KET_LUAN_DO_AN_COURSEGUARD.md")
]

# Read markdown files and store them
embedded_docs = []
for doc_id, display_title, filename in files_in_order:
    filepath = os.path.join(report_dir, filename)
    if os.path.exists(filepath):
        with open(filepath, "r", encoding="utf-8") as f:
            content = f.read()
        escaped_content = html.escape(content)
        embedded_docs.append({
            "id": doc_id,
            "title": display_title,
            "content": escaped_content
        })
        print(f"Loaded: {filename}")
    else:
        print(f"Warning: File not found: {filename}")

# HTML Template
html_template = """<!DOCTYPE html>
<html lang="vi" data-theme="dark">
<head>
    <meta charset="UTF-8">
    <meta name="viewport" content="width=device-width, initial-scale=1.0">
    <meta name="description" content="Báo cáo kỹ thuật tổng hợp của đồ án CourseGuard - Hệ thống quản lý khóa học và thi trực tuyến chống gian lận.">
    <title>Báo Cáo Tổng Hợp Đồ Án - CourseGuard</title>
    
    <!-- Google Fonts -->
    <link href="https://fonts.googleapis.com/css2?family=Outfit:wght@300;400;500;600;700;800&family=Plus+Jakarta+Sans:wght@300;400;500;600;700;800&display=swap" rel="stylesheet">
    
    <!-- FontAwesome for icons -->
    <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css">

    <!-- CSS Theme Variables and Styling -->
    <style>
        :root {
            /* Dark Theme Colors (Default) */
            --bg-base: #030712;
            --bg-sidebar: #090d16;
            --bg-card: rgba(17, 24, 39, 0.7);
            --border-color: rgba(255, 255, 255, 0.08);
            --text-primary: #f3f4f6;
            --text-secondary: #9ca3af;
            --accent-cyan: #06b6d4;
            --accent-indigo: #6366f1;
            --accent-green: #10b981;
            --accent-amber: #f59e0b;
            --accent-red: #ef4444;
            --gradient-primary: linear-gradient(135deg, #06b6d4 0%, #6366f1 100%);
            --shadow-glow: 0 8px 32px 0 rgba(6, 182, 212, 0.15);
            --font-mono: 'Fira Code', 'Courier New', Courier, monospace;
            --sidebar-width: 320px;
        }

        [data-theme="light"] {
            /* Light Theme Colors */
            --bg-base: #f8fafc;
            --bg-sidebar: #ffffff;
            --bg-card: rgba(255, 255, 255, 0.9);
            --border-color: rgba(0, 0, 0, 0.08);
            --text-primary: #0f172a;
            --text-secondary: #475569;
            --accent-cyan: #0891b2;
            --accent-indigo: #4f46e5;
            --accent-green: #16a34a;
            --accent-amber: #d97706;
            --accent-red: #dc2626;
            --gradient-primary: linear-gradient(135deg, #0891b2 0%, #4f46e5 100%);
            --shadow-glow: 0 8px 32px 0 rgba(8, 145, 178, 0.08);
        }

        * {
            box-sizing: border-box;
            margin: 0;
            padding: 0;
        }

        body {
            background-color: var(--bg-base);
            color: var(--text-primary);
            font-family: 'Plus Jakarta Sans', sans-serif;
            line-height: 1.6;
            display: flex;
            min-height: 100vh;
            overflow-x: hidden;
            background-image: 
                radial-gradient(circle at 10% 20%, rgba(6, 182, 212, 0.04) 0%, transparent 40%),
                radial-gradient(circle at 90% 80%, rgba(99, 102, 241, 0.04) 0%, transparent 40%);
            background-attachment: fixed;
            transition: background-color 0.3s ease, color 0.3s ease;
        }

        /* Sidebar Style */
        .sidebar {
            width: var(--sidebar-width);
            background-color: var(--bg-sidebar);
            border-right: 1px solid var(--border-color);
            position: fixed;
            top: 0;
            bottom: 0;
            left: 0;
            display: flex;
            flex-direction: column;
            z-index: 100;
            transition: background-color 0.3s ease, border-color 0.3s ease;
        }

        .sidebar-header {
            padding: 2rem 1.5rem 1.5rem 1.5rem;
            border-bottom: 1px solid var(--border-color);
        }

        .sidebar-logo {
            font-family: 'Outfit', sans-serif;
            font-size: 1.5rem;
            font-weight: 800;
            background: var(--gradient-primary);
            -webkit-background-clip: text;
            background-clip: text;
            -webkit-text-fill-color: transparent;
            display: flex;
            align-items: center;
            gap: 0.75rem;
            margin-bottom: 0.25rem;
        }

        .sidebar-subtitle {
            font-size: 0.75rem;
            color: var(--text-secondary);
            text-transform: uppercase;
            letter-spacing: 0.08em;
            font-weight: 600;
        }

        .search-container {
            margin-top: 1rem;
            position: relative;
        }

        .search-input {
            width: 100%;
            background: rgba(0, 0, 0, 0.15);
            border: 1px solid var(--border-color);
            border-radius: 0.5rem;
            padding: 0.5rem 0.75rem 0.5rem 2.25rem;
            color: var(--text-primary);
            font-family: inherit;
            font-size: 0.85rem;
            outline: none;
            transition: border-color 0.2s ease;
        }

        [data-theme="light"] .search-input {
            background: rgba(0, 0, 0, 0.03);
        }

        .search-input:focus {
            border-color: var(--accent-cyan);
        }

        .search-icon {
            position: absolute;
            left: 0.75rem;
            top: 50%;
            transform: translateY(-50%);
            color: var(--text-secondary);
            font-size: 0.85rem;
        }

        /* Sidebar Navigation */
        .sidebar-nav {
            flex: 1;
            overflow-y: auto;
            padding: 1.5rem 1rem;
        }

        .nav-group {
            margin-bottom: 1.5rem;
        }

        .nav-item {
            display: flex;
            align-items: center;
            padding: 0.65rem 0.75rem;
            border-radius: 0.5rem;
            color: var(--text-secondary);
            text-decoration: none;
            font-size: 0.9rem;
            font-weight: 500;
            margin-bottom: 0.25rem;
            transition: color 0.2s, background-color 0.2s;
            cursor: pointer;
        }

        .nav-item:hover {
            color: var(--text-primary);
            background: rgba(255, 255, 255, 0.03);
        }

        [data-theme="light"] .nav-item:hover {
            background: rgba(0, 0, 0, 0.02);
        }

        .nav-item.active {
            color: var(--text-primary);
            background: var(--gradient-primary);
            font-weight: 600;
            box-shadow: var(--shadow-glow);
        }

        .nav-item.active i {
            color: #ffffff;
        }

        .nav-item i {
            margin-right: 0.75rem;
            font-size: 1rem;
            width: 1.25rem;
            text-align: center;
        }

        .sidebar-footer {
            padding: 1rem 1.5rem;
            border-top: 1px solid var(--border-color);
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .theme-toggle-btn {
            background: none;
            border: 1px solid var(--border-color);
            color: var(--text-primary);
            padding: 0.4rem 0.8rem;
            border-radius: 0.375rem;
            font-size: 0.8rem;
            cursor: pointer;
            display: flex;
            align-items: center;
            gap: 0.5rem;
            transition: background 0.2s;
        }

        .theme-toggle-btn:hover {
            background: rgba(255, 255, 255, 0.05);
        }

        [data-theme="light"] .theme-toggle-btn:hover {
            background: rgba(0, 0, 0, 0.03);
        }

        /* Main Content Style */
        .main-content {
            margin-left: var(--sidebar-width);
            flex: 1;
            padding: 3rem 4rem;
            max-width: 1300px;
        }

        /* Progress Bar */
        .scroll-progress {
            position: fixed;
            top: 0;
            left: var(--sidebar-width);
            right: 0;
            height: 3px;
            background: linear-gradient(to right, var(--accent-cyan), var(--accent-indigo));
            width: 0%;
            z-index: 1000;
        }

        /* Content Markdown Styling */
        section {
            margin-bottom: 5rem;
            scroll-margin-top: 2rem;
        }

        .section-header {
            margin-bottom: 2rem;
            border-bottom: 1px solid var(--border-color);
            padding-bottom: 1rem;
        }

        .section-title {
            font-family: 'Outfit', sans-serif;
            font-size: 2.25rem;
            font-weight: 800;
            background: var(--gradient-primary);
            -webkit-background-clip: text;
            background-clip: text;
            -webkit-text-fill-color: transparent;
            letter-spacing: -0.02em;
        }

        .card {
            background: var(--bg-card);
            border: 1px solid var(--border-color);
            border-radius: 1.25rem;
            padding: 2.5rem;
            backdrop-filter: blur(16px);
            box-shadow: 0 4px 30px rgba(0, 0, 0, 0.2);
            transition: border-color 0.3s ease;
        }

        [data-theme="light"] .card {
            box-shadow: 0 4px 30px rgba(0, 0, 0, 0.03);
        }

        /* Markdown Typography */
        .markdown-body h1, .markdown-body h2, .markdown-body h3, .markdown-body h4 {
            font-family: 'Outfit', sans-serif;
            color: var(--text-primary);
            margin-top: 2rem;
            margin-bottom: 1rem;
            font-weight: 700;
        }

        .markdown-body h1 { font-size: 1.75rem; border-bottom: 1px solid var(--border-color); padding-bottom: 0.5rem; }
        .markdown-body h2 { font-size: 1.45rem; color: var(--accent-cyan); }
        .markdown-body h3 { font-size: 1.2rem; }
        .markdown-body h4 { font-size: 1.05rem; }

        .markdown-body p {
            margin-bottom: 1.25rem;
            color: var(--text-primary);
            opacity: 0.9;
            font-size: 1rem;
            line-height: 1.7;
        }

        .markdown-body ul, .markdown-body ol {
            padding-left: 1.75rem;
            margin-bottom: 1.25rem;
            color: var(--text-primary);
            opacity: 0.9;
        }

        .markdown-body li {
            margin-bottom: 0.5rem;
        }

        .markdown-body strong {
            font-weight: 600;
            color: var(--accent-cyan);
        }

        /* Tables */
        .markdown-body table {
            width: 100%;
            border-collapse: collapse;
            margin: 1.5rem 0;
            font-size: 0.95rem;
            border-radius: 0.75rem;
            overflow: hidden;
            border: 1px solid var(--border-color);
        }

        .markdown-body th {
            background: rgba(255, 255, 255, 0.03);
            color: var(--text-primary);
            font-weight: 600;
            padding: 0.75rem 1rem;
            border-bottom: 2px solid var(--border-color);
            text-transform: uppercase;
            font-size: 0.8rem;
            letter-spacing: 0.05em;
        }

        [data-theme="light"] .markdown-body th {
            background: rgba(0, 0, 0, 0.02);
        }

        .markdown-body td {
            padding: 0.85rem 1rem;
            border-bottom: 1px solid var(--border-color);
            color: var(--text-primary);
            opacity: 0.95;
        }

        .markdown-body tr:last-child td {
            border-bottom: none;
        }

        .markdown-body tr:hover td {
            background: rgba(255, 255, 255, 0.015);
        }

        /* Code and Pre */
        .markdown-body pre {
            background: #090d16;
            border: 1px solid var(--border-color);
            border-radius: 0.75rem;
            padding: 1.25rem;
            overflow-x: auto;
            margin-bottom: 1.5rem;
            transition: background 0.3s;
        }

        [data-theme="light"] .markdown-body pre {
            background: #f1f5f9;
        }

        .markdown-body code {
            font-family: var(--font-mono);
            font-size: 0.85rem;
            color: var(--accent-cyan);
            background: rgba(6, 182, 212, 0.08);
            padding: 0.2rem 0.4rem;
            border-radius: 0.25rem;
        }

        .markdown-body pre code {
            color: var(--text-primary);
            background: none;
            padding: 0;
            font-size: 0.85rem;
        }

        /* Blockquotes and Alerts */
        .markdown-body blockquote {
            border-left: 4px solid var(--accent-indigo);
            background: rgba(99, 102, 241, 0.03);
            padding: 0.75rem 1.25rem;
            margin: 1.5rem 0;
            border-radius: 0.375rem;
        }

        /* Alerts Styling */
        .alert-box {
            border-left-width: 4px;
            border-left-style: solid;
            border-radius: 0.5rem;
            padding: 1rem 1.25rem;
            margin: 1.5rem 0;
        }
        .alert-box-title {
            font-weight: 700;
            font-size: 0.95rem;
            margin-bottom: 0.25rem;
            display: flex;
            align-items: center;
            gap: 0.5rem;
        }
        .alert-box-desc {
            font-size: 0.9rem;
            opacity: 0.9;
        }

        .alert-note {
            background: rgba(99, 102, 241, 0.05);
            border-left-color: var(--accent-indigo);
        }
        .alert-note .alert-box-title { color: #818cf8; }

        .alert-tip {
            background: rgba(16, 185, 129, 0.05);
            border-left-color: var(--accent-green);
        }
        .alert-tip .alert-box-title { color: #34d399; }

        .alert-important {
            background: rgba(6, 182, 212, 0.05);
            border-left-color: var(--accent-cyan);
        }
        .alert-important .alert-box-title { color: #22d3ee; }

        .alert-warning {
            background: rgba(245, 158, 11, 0.05);
            border-left-color: var(--accent-amber);
        }
        .alert-warning .alert-box-title { color: #fbbf24; }

        .alert-caution {
            background: rgba(239, 68, 68, 0.05);
            border-left-color: var(--accent-red);
        }
        .alert-caution .alert-box-title { color: #f87171; }

        /* Mermaid diagrams container */
        .mermaid {
            background: #020617 !important;
            border: 1px solid var(--border-color);
            border-radius: 0.75rem;
            padding: 1.5rem;
            margin: 1.5rem 0;
            display: flex;
            justify-content: center;
            overflow-x: auto;
        }

        /* Top Title Card */
        .title-card {
            background: var(--gradient-primary);
            border-radius: 1.25rem;
            padding: 3rem;
            color: #ffffff;
            margin-bottom: 3rem;
            box-shadow: var(--shadow-glow);
            position: relative;
            overflow: hidden;
        }

        .title-card::after {
            content: "";
            position: absolute;
            top: 0;
            right: 0;
            bottom: 0;
            left: 0;
            background: radial-gradient(circle at 80% 20%, rgba(255, 255, 255, 0.15) 0%, transparent 50%);
            pointer-events: none;
        }

        .title-card h1 {
            font-family: 'Outfit', sans-serif;
            font-size: 3rem;
            font-weight: 800;
            margin-bottom: 0.5rem;
            letter-spacing: -0.03em;
        }

        .title-card p {
            font-size: 1.2rem;
            opacity: 0.9;
        }

        /* Scrollbar styles */
        ::-webkit-scrollbar {
            width: 8px;
            height: 8px;
        }
        ::-webkit-scrollbar-track {
            background: transparent;
        }
        ::-webkit-scrollbar-thumb {
            background: var(--border-color);
            border-radius: 4px;
        }
        ::-webkit-scrollbar-thumb:hover {
            background: var(--text-secondary);
        }

        /* Responsive */
        @media (max-width: 1024px) {
            .sidebar {
                transform: translateX(-100%);
                transition: transform 0.3s ease;
            }
            .sidebar.open {
                transform: translateX(0);
            }
            .main-content {
                margin-left: 0;
                padding: 2rem;
            }
            .scroll-progress {
                left: 0;
            }
        }
    </style>
</head>
<body>

    <!-- Scroll Progress Indicator -->
    <div class="scroll-progress" id="progressBar"></div>

    <!-- Sidebar TOC -->
    <aside class="sidebar">
        <div class="sidebar-header">
            <div class="sidebar-logo">
                <i class="fa-solid fa-shield-halved"></i> CourseGuard
            </div>
            <div class="sidebar-subtitle">Báo cáo tổng kết đồ án</div>
            
            <!-- Search bar -->
            <div class="search-container">
                <i class="fa-solid fa-magnifying-glass search-icon"></i>
                <input type="text" class="search-input" id="searchInput" placeholder="Tìm kiếm mục lục...">
            </div>
        </div>
        
        <!-- Navigation scroll area -->
        <nav class="sidebar-nav" id="navLinks">
            <div class="nav-group">
                <!-- Links will be generated here dynamically -->
            </div>
        </nav>

        <div class="sidebar-footer">
            <button class="theme-toggle-btn" id="themeToggleBtn">
                <i class="fa-solid fa-sun"></i> <span>Giao diện sáng</span>
            </button>
        </div>
    </aside>

    <!-- Main Content Area -->
    <main class="main-content">
        <!-- Title Banner Card -->
        <div class="title-card">
            <h1>Báo Cáo Tổng Hợp Đồ Án</h1>
            <p>Hệ thống Quản lý Khóa học & Thi Trực tuyến Chống Gian lận CourseGuard (WinForms - PostgreSQL/Supabase)</p>
            <div style="margin-top: 1.5rem; font-size: 0.85rem; opacity: 0.8; display: flex; gap: 2rem;">
                <span><i class="fa-solid fa-laptop-code"></i> <strong>Công nghệ:</strong> .NET 10 (C# WinForms), Npgsql, Supabase, SMTP</span>
                <span><i class="fa-solid fa-calendar-days"></i> <strong>Ngày cập nhật:</strong> 2026-06-03</span>
            </div>
        </div>

        <!-- Sections Container -->
        <div id="sectionsContainer">
            <!-- Raw Markdown Script Templates (HTML Escaped) -->
"""

# Append script templates with escaped markdown contents
for doc in embedded_docs:
    html_template += f'            <script type="text/markdown" id="src-{doc["id"]}" data-title="{doc["title"]}">{doc["content"]}</script>\n'

html_template += """        </div>

        <!-- Footer -->
        <footer style="margin-top: 5rem; border-top: 1px solid var(--border-color); padding-top: 2rem; text-align: center; font-size: 0.85rem; color: var(--text-secondary);">
            <p>&copy; 2026 CourseGuard Project. Học phần Lập trình mạng căn bản NT106.</p>
            <p style="margin-top: 0.25rem;">Tài liệu được kết xuất tự động từ các tệp báo cáo Markdown trong dự án.</p>
        </footer>
    </main>

    <!-- Scripts -->
    <!-- Marked.js for Markdown parsing -->
    <script src="https://cdn.jsdelivr.net/npm/marked/marked.min.js"></script>
    
    <!-- Mermaid.js for drawing diagrams -->
    <script src="https://cdn.jsdelivr.net/npm/mermaid/dist/mermaid.min.js"></script>

    <script>
        // Get theme first to initialize Mermaid with proper theme
        const savedTheme = localStorage.getItem('theme') || 'dark';
        document.documentElement.setAttribute('data-theme', savedTheme);

        // Initialize Mermaid
        mermaid.initialize({
            startOnLoad: false,
            theme: savedTheme === 'dark' ? 'dark' : 'default',
            securityLevel: 'loose',
            flowchart: { useMaxWidth: true, htmlLabels: true }
        });

        // Config marked
        marked.setOptions({
            breaks: true,
            gfm: true
        });

        // Custom parser for Alert boxes in Markdown
        function parseAlerts(html) {
            const alertRegex = /<blockquote>\\s*<p>\\s*\\[!(NOTE|TIP|IMPORTANT|WARNING|CAUTION)\\]([\\s\\S]*?)<\\/p>\\s*<\\/blockquote>/gi;
            
            return html.replace(alertRegex, function(match, type, content) {
                type = type.toUpperCase();
                let icon = 'fa-info-circle';
                let title = 'NOTE';
                
                switch(type) {
                    case 'TIP':
                        icon = 'fa-lightbulb';
                        title = 'MẸO / GỢI Ý';
                        break;
                    case 'IMPORTANT':
                        icon = 'fa-exclamation-circle';
                        title = 'QUAN TRỌNG';
                        break;
                    case 'WARNING':
                        icon = 'fa-exclamation-triangle';
                        title = 'CẢNH BÁO';
                        break;
                    case 'CAUTION':
                        icon = 'fa-fire-alt';
                        title = 'CHÚ Ý NGUY HIỂM';
                        break;
                    default:
                        icon = 'fa-info-circle';
                        title = 'GHI CHÚ';
                }
                
                return `<div class="alert-box alert-${type.toLowerCase()}">
                    <div class="alert-box-title">
                        <i class="fa-solid ${icon}"></i> ${title}
                    </div>
                    <div class="alert-box-desc">${content.trim()}</div>
                </div>`;
            });
        }

        // Helper to decode HTML entities in markdown
        function decodeHtml(html) {
            const txt = document.createElement("textarea");
            txt.innerHTML = html;
            return txt.value;
        }

        // Render Markdown documents
        const sectionsContainer = document.getElementById('sectionsContainer');
        const navLinksGroup = document.querySelector('#navLinks .nav-group');
        const searchInput = document.getElementById('searchInput');
        const themeToggleBtn = document.getElementById('themeToggleBtn');
        const progressBar = document.getElementById('progressBar');
        
        const scriptTags = document.querySelectorAll('script[type="text/markdown"]');
        const renderedSections = [];

        scriptTags.forEach((script) => {
            const docId = script.id.replace('src-', '');
            const title = script.getAttribute('data-title');
            const markdownText = decodeHtml(script.textContent);

            // Render markdown to HTML
            let renderedHtml = marked.parse(markdownText);
            
            // Postprocess alerts
            renderedHtml = parseAlerts(renderedHtml);

            // Re-map absolute local file links to match local file structure or styling
            // Example: replace file:///... with a styled link or internal jump if appropriate
            
            // Postprocess Mermaid blocks
            // marked outputs <pre><code class="language-mermaid">...</code></pre>
            const tempDiv = document.createElement('div');
            tempDiv.innerHTML = renderedHtml;
            const mermaidCodes = tempDiv.querySelectorAll('pre code.language-mermaid');
            
            mermaidCodes.forEach((codeNode) => {
                const preNode = codeNode.parentNode;
                const mermaidDiv = document.createElement('div');
                mermaidDiv.className = 'mermaid';
                // HTML entity decode for mermaid diagrams
                mermaidDiv.textContent = codeNode.textContent;
                preNode.replaceWith(mermaidDiv);
            });

            // Create Section Node
            const sectionNode = document.createElement('section');
            sectionNode.id = docId;
            sectionNode.className = 'section-block';
            
            sectionNode.innerHTML = `
                <div class="section-header">
                    <h2 class="section-title">${title}</h2>
                </div>
                <div class="card markdown-body">
                    ${tempDiv.innerHTML}
                </div>
            `;
            
            sectionsContainer.appendChild(sectionNode);
            renderedSections.push(sectionNode);

            // Add navigation link
            const navLink = document.createElement('a');
            navLink.className = 'nav-item';
            navLink.setAttribute('data-target', docId);
            
            // Set icon based on index or title
            let icon = 'fa-file-lines';
            if (docId.includes('gioi-thieu')) icon = 'fa-circle-info';
            else if (docId.includes('chuc-nang')) icon = 'fa-list-check';
            else if (docId.includes('usecase')) icon = 'fa-diagram-project';
            else if (docId.includes('kien-truc')) icon = 'fa-cubes';
            else if (docId.includes('giao-tiep')) icon = 'fa-network-wired';
            else if (docId.includes('flow')) icon = 'fa-route';
            else if (docId.includes('cai-dat')) icon = 'fa-download';
            else if (docId.includes('ket-qua')) icon = 'fa-square-poll-vertical';
            else if (docId.includes('ket-luan')) icon = 'fa-flag-checkered';

            navLink.innerHTML = `<i class="fa-solid ${icon}"></i> <span>${title}</span>`;
            navLinksGroup.appendChild(navLink);

            // Click navigation
            navLink.addEventListener('click', (e) => {
                e.preventDefault();
                const targetNode = document.getElementById(docId);
                targetNode.scrollIntoView({ behavior: 'smooth', block: 'start' });
                
                // Set active class
                document.querySelectorAll('.nav-item').forEach(item => item.classList.remove('active'));
                navLink.classList.add('active');
            });
        });

        // Initialize Mermaid rendering after injecting elements
        mermaid.run({
            nodes: document.querySelectorAll('.mermaid')
        });

        // Scrollspy & Progress bar logic
        window.addEventListener('scroll', () => {
            let currentActive = '';
            const scrollPos = window.scrollY + 100; // Offset

            renderedSections.forEach((section) => {
                const sectionTop = section.offsetTop;
                if (scrollPos >= sectionTop) {
                    currentActive = section.id;
                }
            });

            if (currentActive) {
                document.querySelectorAll('.nav-item').forEach((item) => {
                    item.classList.remove('active');
                    if (item.getAttribute('data-target') === currentActive) {
                        item.classList.add('active');
                    }
                });
            }

            // Progress bar
            const windowHeight = document.documentElement.scrollHeight - document.documentElement.clientHeight;
            const scrollPercent = (window.scrollY / windowHeight) * 100;
            progressBar.style.width = scrollPercent + '%';
        });

        // Trigger first scroll spy event
        window.dispatchEvent(new Event('scroll'));

        // Search Filter
        searchInput.addEventListener('input', (e) => {
            const query = e.target.value.toLowerCase().trim();
            const navItems = document.querySelectorAll('.nav-item');

            navItems.forEach((item) => {
                const text = item.textContent.toLowerCase();
                const targetId = item.getAttribute('data-target');
                const targetSection = document.getElementById(targetId);

                if (text.includes(query)) {
                    item.style.display = 'flex';
                    if (targetSection) targetSection.style.display = 'block';
                } else {
                    item.style.display = 'none';
                    if (targetSection) targetSection.style.display = 'none';
                }
            });
        });

        // Theme Toggle (Dark / Light)
        themeToggleBtn.addEventListener('click', () => {
            const currentTheme = document.documentElement.getAttribute('data-theme');
            let newTheme = 'dark';
            let btnHtml = '<i class="fa-solid fa-sun"></i> <span>Giao diện sáng</span>';

            if (currentTheme === 'dark') {
                newTheme = 'light';
                btnHtml = '<i class="fa-solid fa-moon"></i> <span>Giao diện tối</span>';
            }

            document.documentElement.setAttribute('data-theme', newTheme);
            themeToggleBtn.innerHTML = btnHtml;
            
            // Re-render mermaid diagrams to match theme (forces reload)
            window.location.reload(); 
        });

        // Load theme and scroll position on window load
        window.addEventListener('load', () => {
            const savedTheme = localStorage.getItem('theme') || 'dark';
            document.documentElement.setAttribute('data-theme', savedTheme);
            if (savedTheme === 'light') {
                themeToggleBtn.innerHTML = '<i class="fa-solid fa-moon"></i> <span>Giao diện tối</span>';
            }
            
            // Restore scroll position if saved
            const savedScroll = sessionStorage.getItem('report_scroll_pos');
            if (savedScroll) {
                window.scrollTo(0, parseInt(savedScroll, 10));
                sessionStorage.removeItem('report_scroll_pos');
            }
        });

        // Save theme and scroll position before unload
        window.addEventListener('beforeunload', () => {
            const currentTheme = document.documentElement.getAttribute('data-theme');
            localStorage.setItem('theme', currentTheme);
            sessionStorage.setItem('report_scroll_pos', window.scrollY);
        });
    </script>
</body>
</html>
"""

# Write combined HTML file
with open(output_file, "w", encoding="utf-8") as f:
    f.write(html_template)

print("Successfully compiled report.html!")
