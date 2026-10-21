import * as fileSystem from 'node:fs';
import path from 'node:path';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';

const repositoryDirectory = fileURLToPath(new URL('../../', import.meta.url));
const websiteDirectory = path.join(repositoryDirectory, 'WebRPGPage');
const unityDirectory = path.join(repositoryDirectory, 'artifacts/unity-web');
const hostingDirectory = path.join(repositoryDirectory, 'artifacts/firebase-hosting');
const publicDirectory = path.join(hostingDirectory, 'public');
const reservedNames = new Set(['index.html', 'build', 'streamingassets', 'templatedata']);

function listFiles(directory, relativeDirectory = '') {
    return fileSystem.readdirSync(path.join(directory, relativeDirectory), { withFileTypes: true })
        .flatMap(entry => {
            const relativePath = path.join(relativeDirectory, entry.name);
            if (entry.isSymbolicLink() || entry.name.startsWith('.')) {
                throw new Error('Hidden files and symbolic links are not allowed: ' + relativePath);
            }
            if (entry.isDirectory()) {
                return listFiles(directory, relativePath);
            }
            if (!entry.isFile()) {
                throw new Error('Unsupported file: ' + relativePath);
            }
            return [relativePath];
        });
}

const generatedFiles = listFiles(unityDirectory);
for (const suffix of ['.loader.js', '.framework.js', '.data', '.wasm']) {
    if (!generatedFiles.some(file => file.startsWith('Build' + path.sep) && file.endsWith(suffix))) {
        throw new Error('Missing Unity Build output: ' + suffix);
    }
}
if (!generatedFiles.includes('index.html') ||
    generatedFiles.some(file => /\.(br|gz|unityweb)$/.test(file))) {
    throw new Error('Expected an index page and uncompressed Unity output.');
}

const websiteFiles = listFiles(path.join(websiteDirectory, 'public'));
for (const relativePath of websiteFiles) {
    const firstSegment = relativePath.split(path.sep)[0].toLowerCase();
    if (reservedNames.has(firstSegment) || generatedFiles.includes(relativePath)) {
        throw new Error('Website file conflicts with Unity output: ' + relativePath);
    }
}

fileSystem.mkdirSync(hostingDirectory);
fileSystem.cpSync(unityDirectory, publicDirectory, { recursive: true });
for (const relativePath of websiteFiles) {
    const destinationPath = path.join(publicDirectory, relativePath);
    fileSystem.mkdirSync(path.dirname(destinationPath), { recursive: true });
    fileSystem.copyFileSync(path.join(websiteDirectory, 'public', relativePath), destinationPath);
}
fileSystem.copyFileSync(
    path.join(websiteDirectory, 'firebase.json'),
    path.join(hostingDirectory, 'firebase.json')
);

const checksumLines = listFiles(hostingDirectory).sort().map(relativePath => {
    const checksum = createHash('sha256')
        .update(fileSystem.readFileSync(path.join(hostingDirectory, relativePath)))
        .digest('hex');
    return checksum + '  ' + relativePath.split(path.sep).join('/');
});
fileSystem.writeFileSync(path.join(hostingDirectory, 'SHA256SUMS'), checksumLines.join('\n') + '\n');
console.log('Prepared Firebase Hosting artifact with ' + checksumLines.length + ' files.');
