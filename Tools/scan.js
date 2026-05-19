const fs = require('fs');
const path = require('path');

// --- Configuration ---

// 1. PROJECT TOGGLES: We only need to control content toggles now.
const PROJECT_TOGGLES = {
    // Note: The key should be the relative path to the project root, which is '.' for the current folder.
    ".": { include: true, content: true }, 
    "root": { include: true, content: false }, 
};

// 2. SCRIPT SETTINGS
const PROJECT_ROOT = '.'; 
const OUTPUT_FOLDER = 'Tools'; 

// Get the project name from the parent directory and create timestamped filenames
const getProjectName = () => {
    const resolvedRoot = path.resolve(PROJECT_ROOT);
    return path.basename(resolvedRoot);
};

const getTimestamp = () => {
    const now = new Date();
    const year = now.getFullYear();
    const month = String(now.getMonth() + 1).padStart(2, '0');
    const day = String(now.getDate()).padStart(2, '0');
    const hours = String(now.getHours()).padStart(2, '0');
    const minutes = String(now.getMinutes()).padStart(2, '0');
    return `${year}-${month}-${day}-${hours}${minutes}`;
};

const PROJECT_NAME = getProjectName();
const TIMESTAMP = getTimestamp();
const OUTPUT_FILE = path.join(OUTPUT_FOLDER, `all_project_files_${PROJECT_NAME}_${TIMESTAMP}.txt`); 
const EXPORT_CSV_FILE = path.join(OUTPUT_FOLDER, `all_project_files_${PROJECT_NAME}_${TIMESTAMP}.csv`); 

// --- Ignore Patterns & File Types ---

const IGNORE_PATTERNS = [
    /node_modules/, /bin/, /obj/, 
    /\.vs/, /\.vscode/, /\.git/, /\.idea/, 
    /\.suo$/i, /\.user$/i, /\.userosscache$/i,
    /\btemp\b/, /\.(pdb|dll|exe|nupkg)$/i, 
    /\.(csv|xls|xlsx)$/i,
    /all_project_files.*\.txt/i, /all_project_files.*\.csv/i, 
    /^Tools\/|^Tools\\/, 
];

const IMAGE_EXTENSIONS = ['.png', '.jpg', '.jpeg', '.gif', '.ico', '.svg', '.webp']; 

// --- MODIFICATION ---
// Config object for managing different header comment styles
// *** FIX: Removed .html and .htm to prevent file corruption ***
const FILE_HEADER_FORMATS = {
    // JS/TS/C# use //
    '.ts':   { prefix: '// FILE: ', suffix: '' },
    '.tsx':  { prefix: '// FILE: ', suffix: '' },
    '.cs':   { prefix: '// FILE: ', suffix: '' },
    
    // SQL/CSS use /* ... */
    '.sql':  { prefix: '/* FILE: ', suffix: ' */' },
    '.css':  { prefix: '/* FILE: ', suffix: ' */' },
};
// --- END MODIFICATION ---

const TEXT_EXTENSIONS = [
    // C# / .NET
    '.cs', '.cshtml', '.razor', '.vb', 
    '.sln', '.csproj', '.props', '.targets', 
    // Web (JS, TS, CSS, HTML)
    '.js', '.ts', '.jsx', '.tsx', 
    '.css', '.scss', '.less', '.sass',
    '.html', '.htm',
    // Config and data
    '.json', '.xml', '.config', '.yml', '.yaml', '.toml', 
    // Other
    '.sql', 
    '.md', '.txt', '.log', '.gitignore', 'dockerfile', 
    '.http', 
];

const SENSITIVE_CONTENT_PATTERNS = [
    { pattern: /ConnectionString\s*=\s*["']([^"']+)["']/gi, replacement: 'ConnectionString="***"' },
    { pattern: /password\s*=\s*["']([^"']+)["']/gi, replacement: 'password="***"' },
    { pattern: /(api[_-]?key|apikey)\s*=\s*["']([^"']+)["']/gi, replacement: '$1="***"' },
    { pattern: /(token|secret)\s*=\s*["']([^"']+)["']/gi, replacement: '$1="***"' },
];

const SENSITIVE_FILE_PATTERNS = [
    /\.env$/i, 
    /secrets\.json$/i,
];


// --- Core Modification Logic ---

/**
 * Updates a file in place to ensure the first line is the correct header.
 * Will FIX an incorrect header format (e.g., // on a .sql file).
 * @param {string} filePath The absolute path to the file.
 * @param {string} relativePath The path relative to the project root.
 */
function ensureFileHeader(filePath, relativePath) {
    const ext = path.extname(filePath).toLowerCase();
    const format = FILE_HEADER_FORMATS[ext];

    // If this file type isn't in our format list, do nothing.
    if (!format) {
        return;
    }

    const fileContent = fs.readFileSync(filePath, 'utf-8');
    const lines = fileContent.split('\n');
    const expectedHeader = `${format.prefix}${relativePath}${format.suffix}`;
    
    if (lines.length === 0) {
        // Empty file, just add the header
        fs.writeFileSync(filePath, expectedHeader + '\n', 'utf-8');
        return;
    }

    const firstLine = lines[0].trim();
    
    // Check if the first line matches *any* known header prefix
    let hasExistingHeader = false;
    for (const key in FILE_HEADER_FORMATS) {
        if (firstLine.startsWith(FILE_HEADER_FORMATS[key].prefix)) {
            hasExistingHeader = true;
            break;
        }
    }

    if (hasExistingHeader) {
        // It has *a* header. Replace it if it's not the correct one.
        if (firstLine !== expectedHeader) {
            lines[0] = expectedHeader; // This replaces the incorrect line
            const newContent = lines.join('\n');
            fs.writeFileSync(filePath, newContent, 'utf-8');
            console.log(`✅ FIXED header for: ${relativePath}`);
        }
    } else {
        // No header found, or first line is content. Insert the correct one.
        lines.unshift(expectedHeader);
        const newContent = lines.join('\n');
        fs.writeFileSync(filePath, newContent, 'utf-8');
        console.log(`✅ Inserted header for: ${relativePath}`);
    }
}

// --- Helper Functions ---

/**
 * Checks if a file path should be ignored.
 */
function shouldIgnore(filePath) {
    const normalizedPath = path.normalize(filePath).replace(/\\/g, '/');
    if (IGNORE_PATTERNS.some(pattern => pattern.test(normalizedPath))) {
        return true;
    }
    const topLevelToggle = PROJECT_TOGGLES['.'];
    if (topLevelToggle && topLevelToggle.include === false) {
        return true;
    }
    return false;
}

/**
 * Checks if content should be included.
 */
function shouldIncludeContent(filePath) {
    return PROJECT_TOGGLES['.'].content;
}

const isImage = (filePath) => IMAGE_EXTENSIONS.includes(path.extname(filePath).toLowerCase());

const isHeaderManagedFile = (filePath) => {
    const ext = path.extname(filePath).toLowerCase();
    return FILE_HEADER_FORMATS.hasOwnProperty(ext);
};

/**
 * Reads file content, redacts sensitive information, and
 * REMOVES any known file path header line for clean extraction.
 * @param {string} filePath - The full path to the file.
 * @returns {string} The processed content.
 */
const readFileContent = (filePath) => {
    try {
        const fileExt = path.extname(filePath).toLowerCase();
        const fileName = path.basename(filePath);
        
        if (SENSITIVE_FILE_PATTERNS.some(pattern => pattern.test(fileName))) {
            return '[SENSITIVE CONFIG FILE - CONTENT FULLY MASKED]';
        }

        if (TEXT_EXTENSIONS.includes(fileExt) || !fileExt && !isImage(filePath)) { 
            let content = fs.readFileSync(filePath, 'utf-8');

            // Check if we manage this file type at all
            if (isHeaderManagedFile(filePath)) { 
                const lines = content.split('\n');
                const firstLine = lines[0] ? lines[0].trim() : '';
        
                // Check if the first line starts with *any* known prefix
                for (const key in FILE_HEADER_FORMATS) {
                    if (firstLine.startsWith(FILE_HEADER_FORMATS[key].prefix)) {
                        content = lines.slice(1).join('\n');
                        break;
                    }
                }
            }

            // Redact sensitive values within the content
            SENSITIVE_CONTENT_PATTERNS.forEach(({ pattern, replacement }) => {
                content = content.replace(pattern, replacement);
            });
            
            return content;
        }

        if (isImage(filePath)) {
            return '[IMAGE FILE]';
        }
        
        // Fallback: check for binary content
        const buffer = Buffer.alloc(512);
        const fd = fs.openSync(filePath, 'r');
        const bytesRead = fs.readSync(fd, buffer, 0, 512, 0);
        fs.closeSync(fd);

        if (buffer.slice(0, bytesRead).includes(0)) {
            return '[BINARY CONTENT SKIPPED]';
        }
        
        let content = fs.readFileSync(filePath, 'utf-8');
        SENSITIVE_CONTENT_PATTERNS.forEach(({ pattern, replacement }) => {
            content = content.replace(pattern, replacement);
        });
        return content;

    } catch (err) {
        return `[ERROR READING FILE: ${err.message}]`;
    }
};

// --- Main Logic ---
function scanDirectory(dir, files = []) {
    try {
        const items = fs.readdirSync(dir);
        for (const item of items) {
            const itemPath = path.join(dir, item);
            const relativePath = path.relative(PROJECT_ROOT, itemPath).replace(/\\/g, '/');
            
            if (item === OUTPUT_FOLDER) {
                continue;
            }

            let stat;
            try {
                stat = fs.statSync(itemPath);
            } catch (statErr) {
                continue;
            }

            if (stat.isDirectory() && shouldIgnore(relativePath + '/')) { 
                continue;
            }
            if (stat.isFile() && shouldIgnore(relativePath)) {
                continue;
            }
            
            if (stat.isDirectory()) {
                scanDirectory(itemPath, files);
            } else if (stat.isFile()) {
                
                // 1. Modify file header IN PLACE (if it's a header-managed file)
                if (isHeaderManagedFile(itemPath)) {
                    ensureFileHeader(itemPath, relativePath);
                }

                const includeContent = shouldIncludeContent(relativePath);
                let content = '[CONTENT NOT INCLUDED]';
                let type = 'File';
                
                if (includeContent) {
                    content = readFileContent(itemPath);
                } else if (isImage(itemPath)) {
                    content = '[IMAGE FILE]';
                }

                // (File-typing logic)
                const ext = path.extname(itemPath).toLowerCase();
                if (content === '[SENSITIVE CONFIG FILE - CONTENT FULLY MASKED]') {
                    type = 'SensitiveConfig';
                } else if (content === '[IMAGE FILE]') {
                    type = 'Image';
                } else if (ext === '.sql') {
                    type = 'SQLScript';
                } else if (ext === '.cs') {
                    type = 'CSharp';
                } else if (['.js', '.jsx', '.ts', '.tsx'].includes(ext)) {
                    type = 'Script';
                } else if (ext === '.json') {
                    type = 'JSON';
                } else if (['.html', '.htm', '.cshtml', '.razor'].includes(ext)) {
                    type = 'Markup';
                } else if (['.css', '.scss', '.sass', '.less'].includes(ext)) {
                    type = 'Stylesheet';
                }


                if (!content.startsWith('[ERROR READING FILE') && content !== '[BINARY CONTENT SKIPPED]') {
                    files.push({ path: relativePath, type, content });
                }
            }
        }
    } catch (err) {
        console.error(`Error scanning ${dir}: ${err.message}`);
    }
    return files;
}


function generateOutput(files) {
    files.sort((a, b) => a.path.localeCompare(b.path));

    const summary = [
        `// --- ${PROJECT_NAME.toUpperCase()} Project File Summary ---`, 
        `// Total files: ${files.length}`,
        '// ',
        '// Path                                           | Type',
        '// ----------------------------------------------|-------------',
        ...files.map(f => `// ${f.path.padEnd(46)} | ${f.type}`),
        '// --- End of Summary ---\n\n'
    ].join('\n');

    let contentBlocks = '';
    
    for (const file of files) {
        const isContentBlock = !file.content.startsWith('['); 

        if (isContentBlock) {
            const ext = path.extname(file.path).toLowerCase();
            const format = FILE_HEADER_FORMATS[ext];
            
            if (format) {
                // This file type has a defined header, so re-add it in the correct format
                const header = `${format.prefix}${file.path}${format.suffix}`;
                contentBlocks += `${header}\n${file.content}\n\n---END OF FILE---\n\n`;
            } else {
                // This file type (e.g., .json, .csproj, .html) doesn't get a header,
                // so we use the default // FILE: format for consistency in the *.txt
                contentBlocks += `// FILE: ${file.path}\n${file.content}\n\n---END OF FILE---\n\n`;
            }
        }
    }

    return summary + contentBlocks; 
}

function generateCsvOutput(files) {
    const filePaths = files.map(f => f.path);
    return filePaths.join(',\n') + ',\n'; 
}

function main() {
    console.log('🚀 Starting Project File Lister (v4 - HTML Bug Fixed)...');
    const resolvedRoot = path.resolve(PROJECT_ROOT);
    
    if (!fs.existsSync(resolvedRoot)) {
        console.error(`❌ Project root "${resolvedRoot}" not found.`);
        process.exit(1);
    }
    
    console.log(`Scanning: ${resolvedRoot}`);
    const files = scanDirectory(resolvedRoot);
    
    if (files.length === 0) {
        console.log('✅ No files found.');
        return;
    }
    
    if (!fs.existsSync(OUTPUT_FOLDER)) {
        fs.mkdirSync(OUTPUT_FOLDER);
    }
    
    const output = generateOutput(files);
    try {
        fs.writeFileSync(OUTPUT_FILE, output);
        console.log(`✅ Main output saved to: ${path.resolve(OUTPUT_FILE)}`);
    } catch (err) {
        console.error(`❌ Error saving main output file: ${err.message}`);
    }

    const csvOutput = generateCsvOutput(files);
    try {
        fs.writeFileSync(EXPORT_CSV_FILE, csvOutput);
        console.log(`✅ CSV output saved to: ${path.resolve(EXPORT_CSV_FILE)}`);
    } catch (err) {
        console.error(`❌ Error saving CSV output file: ${err.message}`);
    }

    console.log(`📊 Processed ${files.length} files`);
    console.log('🏁 Done');
}

main();