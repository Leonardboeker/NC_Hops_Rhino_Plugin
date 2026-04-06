const { google } = require('googleapis');
const fs = require('fs');
const path = require('path');
const http = require('http');
const url = require('url');

const SCOPES = ['https://www.googleapis.com/auth/calendar.events'];
const REDIRECT_PORT = 3456;
const REDIRECT_URI = `http://localhost:${REDIRECT_PORT}/oauth2callback`;

function loadCredentials(userDataPath) {
  const credPath = path.join(userDataPath, 'credentials.json');
  if (!fs.existsSync(credPath)) {
    throw new Error(
      `credentials.json not found in: ${userDataPath}\n` +
      'Download it from Google Cloud Console (Desktop app type) and place it there.\n' +
      'Add http://localhost:3456/oauth2callback as an authorized redirect URI.'
    );
  }
  const raw = JSON.parse(fs.readFileSync(credPath, 'utf8'));
  return raw.installed || raw.web;
}

function loadToken(userDataPath) {
  const tokenPath = path.join(userDataPath, 'token.json');
  if (!fs.existsSync(tokenPath)) return null;
  return JSON.parse(fs.readFileSync(tokenPath, 'utf8'));
}

function saveToken(userDataPath, token) {
  const tokenPath = path.join(userDataPath, 'token.json');
  fs.writeFileSync(tokenPath, JSON.stringify(token, null, 2));
}

function getAuthClient(credentials) {
  return new google.auth.OAuth2(
    credentials.client_id,
    credentials.client_secret,
    REDIRECT_URI
  );
}

function waitForCode() {
  return new Promise((resolve, reject) => {
    const server = http.createServer((req, res) => {
      const parsed = url.parse(req.url, true);
      if (parsed.pathname === '/oauth2callback') {
        const code = parsed.query.code;
        const error = parsed.query.error;
        res.writeHead(200, { 'Content-Type': 'text/html' });
        if (code) {
          res.end(
            '<html><body style="font-family:sans-serif;background:#1a1a2e;color:#eaeaea;padding:2rem">' +
            '<h2 style="color:#e94560">Authorization successful!</h2>' +
            '<p>You can close this tab and return to the app.</p></body></html>'
          );
          server.close();
          resolve(code);
        } else {
          res.end(
            '<html><body style="font-family:sans-serif;background:#1a1a2e;color:#eaeaea;padding:2rem">' +
            `<h2 style="color:#e94560">Authorization failed</h2><p>${error || 'Unknown error'}</p></body></html>`
          );
          server.close();
          reject(new Error(`OAuth error: ${error || 'no code returned'}`));
        }
      }
    });
    server.listen(REDIRECT_PORT, '127.0.0.1', () => {});
    server.on('error', (err) => reject(new Error(`OAuth server error: ${err.message}`)));
  });
}

async function authorize(userDataPath, shell) {
  const credentials = loadCredentials(userDataPath);
  const oauth2Client = getAuthClient(credentials);

  const existingToken = loadToken(userDataPath);
  if (existingToken) {
    oauth2Client.setCredentials(existingToken);
    if (existingToken.expiry_date && existingToken.expiry_date < Date.now()) {
      const { credentials: refreshed } = await oauth2Client.refreshAccessToken();
      saveToken(userDataPath, refreshed);
      oauth2Client.setCredentials(refreshed);
    }
    return oauth2Client;
  }

  const authUrl = oauth2Client.generateAuthUrl({
    access_type: 'offline',
    scope: SCOPES,
    prompt: 'consent',
  });

  shell.openExternal(authUrl);
  const code = await waitForCode();
  const { tokens } = await oauth2Client.getToken(code);
  oauth2Client.setCredentials(tokens);
  saveToken(userDataPath, tokens);
  return oauth2Client;
}

async function addTaskToCalendar(task, userDataPath, shell) {
  const auth = await authorize(userDataPath, shell);
  const calendar = google.calendar({ version: 'v3', auth });

  const date = task.due_date || new Date().toISOString().split('T')[0];

  const event = {
    summary: task.title,
    description: task.description || '',
    start: { date },
    end: { date },
  };

  const response = await calendar.events.insert({
    calendarId: 'primary',
    resource: event,
  });

  return { eventId: response.data.id, htmlLink: response.data.htmlLink };
}

module.exports = { addTaskToCalendar };
