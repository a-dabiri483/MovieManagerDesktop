import os
from datetime import datetime

PROJECT_PATH = os.path.dirname(os.path.abspath(__file__))
OUTPUT_FILE = os.path.join(PROJECT_PATH, "ProjectCode.txt")

INCLUDED_EXTENSIONS = ['.cs', '.xaml', '.csproj', '.sln', '.config', '.json', '.xml', '.resx', '.py', '.md', '.txt', '.html', '.css', '.js', '.yaml', '.yml']
EXCLUDED_FOLDERS = ['bin', 'obj', '.git', 'packages', 'node_modules', '__pycache__', '.vs', '.idea']

def should_exclude(folder_name):
    return folder_name in EXCLUDED_FOLDERS

def get_all_code_files(project_path):
    code_files = []
    for root, dirs, files in os.walk(project_path):
        dirs[:] = [d for d in dirs if not should_exclude(d)]
        for file in files:
            file_ext = os.path.splitext(file)[1].lower()
            if file_ext in INCLUDED_EXTENSIONS:
                full_path = os.path.join(root, file)
                code_files.append(full_path)
    return sorted(code_files)

def read_file_content(file_path):
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            return f.read()
    except UnicodeDecodeError:
        try:
            with open(file_path, 'r', encoding='windows-1256') as f:
                return f.read()
        except:
            try:
                with open(file_path, 'r', encoding='latin-1') as f:
                    return f.read()
            except Exception as e:
                return f"[Error reading file: {str(e)}]"

def create_output_content(project_path, code_files):
    output_lines = []
    output_lines.append("=" * 80)
    output_lines.append("Project Code Export")
    output_lines.append("=" * 80)
    output_lines.append(f"Date: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    output_lines.append(f"Project Path: {project_path}")
    output_lines.append(f"Total Files: {len(code_files)}")
    output_lines.append("=" * 80)
    output_lines.append("\n\n")
    
    for index, file_path in enumerate(code_files, 1):
        relative_path = os.path.relpath(file_path, project_path)
        file_size = os.path.getsize(file_path)
        content = read_file_content(file_path)
        
        output_lines.append("=" * 80)
        output_lines.append(f"File {index} of {len(code_files)}")
        output_lines.append("=" * 80)
        output_lines.append(f"Filename: {os.path.basename(file_path)}")
        output_lines.append(f"Relative Path: {relative_path}")
        output_lines.append(f"Size: {file_size} bytes")
        output_lines.append(f"Lines: {content.count(chr(10)) + 1}")
        output_lines.append("=" * 80)
        output_lines.append("\n")
        output_lines.append(content)
        output_lines.append("\n\n")
        output_lines.append("-" * 80)
        output_lines.append("\n\n")
    
    output_lines.append("=" * 80)
    output_lines.append("END OF REPORT")
    output_lines.append("=" * 80)
    
    return "\n".join(output_lines)

def main():
    print("=" * 60)
    print("Starting code extraction...")
    print("=" * 60)
    
    print(f"Project Path: {PROJECT_PATH}")
    print(f"Output File: {OUTPUT_FILE}")
    print("\nSearching for code files...")
    
    code_files = get_all_code_files(PROJECT_PATH)
    
    if not code_files:
        print("No code files found!")
        input("\nPress Enter to exit...")
        return
    
    print(f"Found {len(code_files)} files:")
    for i, file in enumerate(code_files, 1):
        rel_path = os.path.relpath(file, PROJECT_PATH)
        print(f"   {i}. {rel_path}")
    
    print("\nReading file contents...")
    output_content = create_output_content(PROJECT_PATH, code_files)
    
    try:
        with open(OUTPUT_FILE, 'w', encoding='utf-8') as f:
            f.write(output_content)
        
        file_size = os.path.getsize(OUTPUT_FILE)
        print(f"\nSuccess!")
        print(f"Output file size: {file_size:,} bytes ({file_size/1024:.2f} KB)")
        print(f"Saved to: {OUTPUT_FILE}")
        
    except Exception as e:
        print(f"\nError writing output file: {str(e)}")
    
    print("\n" + "=" * 60)
    input("Press Enter to exit...")

if __name__ == "__main__":
    main()