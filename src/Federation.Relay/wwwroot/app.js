// ══════════════════════════════════════════════════
// Agent Federation — Browser UI
// Vanilla ES module, no framework, no bundler.
// ══════════════════════════════════════════════════

// ── Storage ──────────────────────────────────────

const store = {
  get: (key) => JSON.parse(localStorage.getItem('federation_' + key) ?? 'null'),
  set: (key, val) => localStorage.setItem('federation_' + key, JSON.stringify(val)),
  clear: (key) => localStorage.removeItem('federation_' + key),
};

// Keys:
//   'device'           -> { deviceId, deviceToken, deviceName, relayUrl }
//   'activeRoom'       -> { roomId, roomName }
//   'activeDiscussion' -> { discussionId, topic }

// ── API ──────────────────────────────────────────

function getRelayUrl() {
  const device = store.get('device');
  return device?.relayUrl ?? window.location.origin;
}

async function api(path, options = {}) {
  const device = store.get('device');
  const headers = { 'Content-Type': 'application/json', ...(options.headers ?? {}) };
  if (device?.deviceId) headers['X-Device-Id'] = device.deviceId;
  if (device?.deviceToken) headers['X-Device-Token'] = device.deviceToken;

  const url = getRelayUrl() + path;
  const response = await fetch(url, { ...options, headers });

  if (!response.ok) {
    let message = `HTTP ${response.status}`;
    try {
      const body = await response.text();
      if (body) message += ': ' + body;
    } catch (_) { /* ignore */ }
    throw new Error(message);
  }

  const text = await response.text();
  return text ? JSON.parse(text) : null;
}

// ── Router ───────────────────────────────────────

let refreshInterval = null;

function navigate(hash) {
  window.location.hash = hash;
}

function route() {
  if (refreshInterval) {
    clearInterval(refreshInterval);
    refreshInterval = null;
  }

  const hash = window.location.hash || '';
  const app = document.getElementById('app');

  if (hash.startsWith('#room/') && hash.includes('/discussion/')) {
    const parts = hash.replace('#room/', '').split('/discussion/');
    renderDiscussion(app, parts[0], parts[1]);
  } else if (hash.startsWith('#room/')) {
    const roomId = hash.replace('#room/', '');
    renderRoom(app, roomId);
  } else if (hash === '#rooms') {
    renderRooms(app);
  } else if (hash === '#setup') {
    renderSetup(app);
  } else {
    // Default: if registered go to rooms, otherwise setup
    const device = store.get('device');
    if (device?.deviceId) {
      renderRooms(app);
    } else {
      renderSetup(app);
    }
  }

  renderNav();
}

window.addEventListener('hashchange', route);
window.addEventListener('DOMContentLoaded', route);

// ── Copy to Clipboard ────────────────────────────

async function copyToClipboard(text, elementId) {
  try {
    await navigator.clipboard.writeText(text);
    const el = document.getElementById(elementId);
    if (el) {
      const prev = el.textContent;
      el.textContent = '\u2713';
      el.classList.add('copy-check');
      setTimeout(() => {
        el.textContent = prev;
        el.classList.remove('copy-check');
      }, 2000);
    }
  } catch (_) { /* ignore */ }
}

function copyableSpan(text, id) {
  return `<span class="copyable" onclick="copyToClipboard('${text}', '${id}')">${truncate(text, 16)} <span id="${id}">&#x1F4CB;</span></span>`;
}

// ── Helpers ──────────────────────────────────────

function truncate(str, len) {
  if (!str) return '';
  return str.length > len ? str.substring(0, len) + '...' : str;
}

function escapeHtml(str) {
  const div = document.createElement('div');
  div.textContent = str;
  return div.innerHTML;
}

function phaseBadge(phase) {
  const map = {
    'Draft': 'draft',
    'ProposalRound': 'proposal',
    'CritiqueRound': 'critique',
    'RevisionRound': 'revision',
    'Vote': 'vote',
    'Synthesis': 'synthesis',
    'Closed': 'closed',
  };
  const cls = map[phase] ?? 'draft';
  return `<span class="badge badge-${cls}">${escapeHtml(phase)}</span>`;
}

function phaseIndex(phase) {
  const phases = ['Draft', 'ProposalRound', 'CritiqueRound', 'RevisionRound', 'Vote', 'Synthesis', 'Closed'];
  return phases.indexOf(phase);
}

// ── Nav ──────────────────────────────────────────

function renderNav() {
  const el = document.getElementById('nav-status');
  const device = store.get('device');
  if (!el) return;

  if (device?.deviceId) {
    el.innerHTML = `
      <span>${escapeHtml(device.deviceName)}</span>
      <span class="status-dot online" id="status-dot"></span>
    `;
    // Ping health check
    api('/health').then(() => {
      const dot = document.getElementById('status-dot');
      if (dot) { dot.className = 'status-dot online'; }
    }).catch(() => {
      const dot = document.getElementById('status-dot');
      if (dot) { dot.className = 'status-dot offline'; }
    });
  } else {
    el.innerHTML = '<span class="text-muted">Not registered</span>';
  }
}

// ── Setup View ───────────────────────────────────

function renderSetup(app) {
  const device = store.get('device');
  const currentUrl = device?.relayUrl ?? window.location.origin;

  app.innerHTML = `
    <div class="card" style="max-width:500px;margin:40px auto;">
      <h2>Setup</h2>
      <p class="text-muted text-sm mb-md">Register your device with a federation relay to get started.</p>

      ${device?.deviceId ? `
        <div class="alert alert-success mb-md">
          Registered as <strong>${escapeHtml(device.deviceName)}</strong>
          <br><span class="text-xs mono">${escapeHtml(device.deviceId)}</span>
        </div>
        <div class="flex gap-sm">
          <button class="btn btn-primary" onclick="navigate('#rooms')">Go to Rooms</button>
          <button class="btn btn-danger" id="btn-reset">Reset Registration</button>
        </div>
      ` : `
        <div class="form-group">
          <label>Relay URL</label>
          <input class="input" type="text" id="relay-url" value="${escapeHtml(currentUrl)}" placeholder="http://localhost:5000">
        </div>
        <div class="form-group">
          <label>Your Name</label>
          <input class="input" type="text" id="device-name" placeholder="Alice">
        </div>
        <div id="setup-error"></div>
        <div class="flex gap-sm mt-md">
          <button class="btn" id="btn-check">Check Connection</button>
          <button class="btn btn-primary" id="btn-register">Register</button>
        </div>
        <div id="setup-result" class="mt-sm text-sm"></div>
      `}
    </div>
  `;

  if (device?.deviceId) {
    document.getElementById('btn-reset')?.addEventListener('click', () => {
      if (confirm('This will remove your local registration. Continue?')) {
        store.clear('device');
        store.clear('activeRoom');
        store.clear('activeDiscussion');
        route();
      }
    });
    return;
  }

  document.getElementById('btn-check')?.addEventListener('click', async () => {
    const result = document.getElementById('setup-result');
    const url = document.getElementById('relay-url').value.replace(/\/+$/, '');
    try {
      const resp = await fetch(url + '/health');
      if (resp.ok) {
        result.innerHTML = '<span style="color:var(--success)">Relay is reachable.</span>';
      } else {
        result.innerHTML = '<span style="color:var(--danger)">Relay returned HTTP ' + resp.status + '.</span>';
      }
    } catch (e) {
      result.innerHTML = '<span style="color:var(--danger)">Cannot connect: ' + escapeHtml(e.message) + '</span>';
    }
  });

  document.getElementById('btn-register')?.addEventListener('click', async () => {
    const url = document.getElementById('relay-url').value.replace(/\/+$/, '');
    const name = document.getElementById('device-name').value.trim();
    const errorEl = document.getElementById('setup-error');

    if (!name) {
      errorEl.innerHTML = '<div class="alert alert-error">Please enter your name.</div>';
      return;
    }

    try {
      errorEl.innerHTML = '';
      const resp = await fetch(url + '/v1/devices/registrations', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ displayName: name }),
      });

      if (!resp.ok) {
        const body = await resp.text();
        errorEl.innerHTML = `<div class="alert alert-error">Registration failed: HTTP ${resp.status}. ${escapeHtml(body)}</div>`;
        return;
      }

      const data = await resp.json();
      store.set('device', {
        deviceId: data.deviceId,
        deviceToken: data.deviceToken,
        deviceName: name,
        relayUrl: url,
      });

      navigate('#rooms');
    } catch (e) {
      errorEl.innerHTML = `<div class="alert alert-error">${escapeHtml(e.message)}</div>`;
    }
  });
}

// ── Rooms View ───────────────────────────────────

async function renderRooms(app) {
  const device = store.get('device');
  if (!device?.deviceId) { navigate('#setup'); return; }

  app.innerHTML = `
    <div class="card mb-md">
      <p class="text-sm text-muted">Your Device ID &mdash; share this to receive room invitations:</p>
      <div class="mt-sm">${copyableSpan(device.deviceId, 'copy-device-id')}</div>
    </div>

    <div class="section-header">
      <h2>Rooms</h2>
      <button class="btn btn-primary btn-sm" id="btn-toggle-new-room">+ New Room</button>
    </div>

    <div id="new-room-form" style="display:none;" class="card mb-md">
      <div class="form-group">
        <label>Room Name</label>
        <input class="input" type="text" id="new-room-name" placeholder="Council Alpha">
      </div>
      <div id="new-room-error"></div>
      <button class="btn btn-primary" id="btn-create-room">Create Room</button>
    </div>

    <div id="rooms-list">
      <div class="empty-state">
        <div class="empty-state-icon">&#x23F3;</div>
        <p>Loading rooms...</p>
      </div>
    </div>
  `;

  document.getElementById('btn-toggle-new-room')?.addEventListener('click', () => {
    const form = document.getElementById('new-room-form');
    form.style.display = form.style.display === 'none' ? 'block' : 'none';
  });

  document.getElementById('btn-create-room')?.addEventListener('click', async () => {
    const name = document.getElementById('new-room-name').value.trim();
    const errorEl = document.getElementById('new-room-error');
    if (!name) {
      errorEl.innerHTML = '<div class="alert alert-error">Enter a room name.</div>';
      return;
    }
    try {
      errorEl.innerHTML = '';
      const room = await api('/v1/rooms', {
        method: 'POST',
        body: JSON.stringify({ name, ownerDeviceId: device.deviceId }),
      });
      store.set('activeRoom', { roomId: room.roomId, roomName: room.name });
      navigate('#room/' + room.roomId);
    } catch (e) {
      errorEl.innerHTML = `<div class="alert alert-error">${escapeHtml(e.message)}</div>`;
    }
  });

  // Fetch rooms
  try {
    const rooms = await api('/v1/rooms');
    const list = document.getElementById('rooms-list');
    if (!rooms || rooms.length === 0) {
      list.innerHTML = `
        <div class="empty-state">
          <div class="empty-state-icon">&#x1F3E0;</div>
          <h3>No rooms yet</h3>
          <p>Create a new room or ask someone to invite your Device ID.</p>
        </div>
      `;
    } else {
      list.innerHTML = rooms.map(r => `
        <div class="card card-clickable" onclick="navigate('#room/${r.roomId}')">
          <h3>${escapeHtml(r.name)}</h3>
          <div class="card-meta">${r.memberCount} member${r.memberCount !== 1 ? 's' : ''}</div>
        </div>
      `).join('');
    }
  } catch (e) {
    document.getElementById('rooms-list').innerHTML = `
      <div class="alert alert-error">Failed to load rooms: ${escapeHtml(e.message)}</div>
    `;
  }
}

// ── Room Detail View ─────────────────────────────

async function renderRoom(app, roomId) {
  const device = store.get('device');
  if (!device?.deviceId) { navigate('#setup'); return; }

  app.innerHTML = `
    <div class="mb-md">
      <button class="btn btn-ghost btn-sm" onclick="navigate('#rooms')">&larr; Back to Rooms</button>
    </div>
    <div id="room-content">
      <div class="empty-state"><p>Loading room...</p></div>
    </div>
  `;

  try {
    const rooms = await api('/v1/rooms');
    const room = rooms?.find(r => r.roomId === roomId);
    const roomName = room?.name ?? 'Room';
    store.set('activeRoom', { roomId, roomName });

    let discussions = [];
    try {
      discussions = await api(`/v1/rooms/${roomId}/discussions`);
    } catch (_) { /* may 404 if not a member yet */ }

    const content = document.getElementById('room-content');
    content.innerHTML = `
      <div class="section-header">
        <h2>${escapeHtml(roomName)}</h2>
      </div>
      <div class="card-meta mb-md">
        Room ID: ${copyableSpan(roomId, 'copy-room-id')}
      </div>

      <div class="card mb-md">
        <h3>Invite by Device ID</h3>
        <div class="grid-2 mt-sm">
          <div class="form-group">
            <label>Device ID</label>
            <input class="input" type="text" id="invite-device-id" placeholder="Paste device ID">
          </div>
          <div class="form-group">
            <label>Role</label>
            <select class="input" id="invite-role">
              <option value="Participant">Participant</option>
              <option value="Synthesizer">Synthesizer</option>
              <option value="Observer">Observer</option>
            </select>
          </div>
        </div>
        <div id="invite-error"></div>
        <button class="btn btn-primary btn-sm" id="btn-invite">Invite</button>
      </div>

      <div class="section-header">
        <h2>Discussions</h2>
        <button class="btn btn-primary btn-sm" id="btn-toggle-new-disc">+ New Discussion</button>
      </div>

      <div id="new-disc-form" style="display:none;" class="card mb-md">
        <div class="form-group">
          <label>Topic / Question</label>
          <textarea class="input" id="new-disc-topic" placeholder="Should we adopt proposal X?"></textarea>
        </div>
        <div id="new-disc-error"></div>
        <button class="btn btn-primary" id="btn-create-disc">Start Discussion</button>
      </div>

      <div id="discussions-list">
        ${discussions.length === 0 ? `
          <div class="empty-state">
            <div class="empty-state-icon">&#x1F4AC;</div>
            <h3>No discussions yet</h3>
            <p>Start a new discussion to begin deliberation.</p>
          </div>
        ` : discussions.map(d => `
          <div class="card card-clickable" onclick="navigate('#room/${roomId}/discussion/${d.discussionId}')">
            <div class="flex" style="justify-content:space-between;align-items:center;">
              <h3>${escapeHtml(d.topic)}</h3>
              ${phaseBadge(d.phase)}
            </div>
            <div class="card-meta mt-sm">Round ${d.currentRound} &middot; ${d.totalSubmissions} submissions</div>
          </div>
        `).join('')}
      </div>
    `;

    // Invite handler
    document.getElementById('btn-invite')?.addEventListener('click', async () => {
      const deviceId = document.getElementById('invite-device-id').value.trim();
      const role = document.getElementById('invite-role').value;
      const errorEl = document.getElementById('invite-error');
      if (!deviceId) {
        errorEl.innerHTML = '<div class="alert alert-error">Enter a Device ID.</div>';
        return;
      }
      try {
        errorEl.innerHTML = '';
        await api(`/v1/rooms/${roomId}/invitations`, {
          method: 'POST',
          body: JSON.stringify({ deviceId, role }),
        });
        errorEl.innerHTML = '<div class="alert alert-success">Invitation sent.</div>';
        document.getElementById('invite-device-id').value = '';
      } catch (e) {
        errorEl.innerHTML = `<div class="alert alert-error">${escapeHtml(e.message)}</div>`;
      }
    });

    // New discussion toggle
    document.getElementById('btn-toggle-new-disc')?.addEventListener('click', () => {
      const form = document.getElementById('new-disc-form');
      form.style.display = form.style.display === 'none' ? 'block' : 'none';
    });

    // Create discussion handler
    document.getElementById('btn-create-disc')?.addEventListener('click', async () => {
      const topic = document.getElementById('new-disc-topic').value.trim();
      const errorEl = document.getElementById('new-disc-error');
      if (!topic) {
        errorEl.innerHTML = '<div class="alert alert-error">Enter a topic.</div>';
        return;
      }
      try {
        errorEl.innerHTML = '';
        const disc = await api(`/v1/rooms/${roomId}/discussions`, {
          method: 'POST',
          body: JSON.stringify({ topic, initiatorDeviceId: device.deviceId }),
        });
        store.set('activeDiscussion', { discussionId: disc.discussionId, topic });
        navigate(`#room/${roomId}/discussion/${disc.discussionId}`);
      } catch (e) {
        errorEl.innerHTML = `<div class="alert alert-error">${escapeHtml(e.message)}</div>`;
      }
    });

  } catch (e) {
    document.getElementById('room-content').innerHTML = `
      <div class="alert alert-error">Failed to load room: ${escapeHtml(e.message)}</div>
    `;
  }
}

// ── Discussion View ──────────────────────────────

async function renderDiscussion(app, roomId, discussionId) {
  const device = store.get('device');
  if (!device?.deviceId) { navigate('#setup'); return; }

  const activeRoom = store.get('activeRoom');
  const roomName = activeRoom?.roomName ?? 'Room';

  app.innerHTML = `
    <div class="mb-md">
      <button class="btn btn-ghost btn-sm" onclick="navigate('#room/${roomId}')">&larr; Back to ${escapeHtml(roomName)}</button>
    </div>
    <div id="disc-content">
      <div class="empty-state"><p>Loading discussion...</p></div>
    </div>
  `;

  await loadDiscussionContent(roomId, discussionId);

  // Auto-refresh every 10 seconds
  refreshInterval = setInterval(() => {
    loadDiscussionContent(roomId, discussionId);
  }, 10000);
}

async function loadDiscussionContent(roomId, discussionId) {
  try {
    const state = await api(`/v1/rooms/${roomId}/discussions/${discussionId}/state`);
    if (!state) return;

    store.set('activeDiscussion', { discussionId, topic: state.topic });

    const phases = ['Draft', 'ProposalRound', 'CritiqueRound', 'RevisionRound', 'Vote', 'Synthesis', 'Closed'];
    const phaseLabels = ['Draft', 'Proposal', 'Critique', 'Revision', 'Vote', 'Synthesis', 'Closed'];
    const currentIdx = phaseIndex(state.phase);

    const phaseBar = phases.map((p, i) => {
      let cls = 'phase-step';
      if (i < currentIdx) cls += ' completed';
      else if (i === currentIdx) cls += ' active';
      return `<div class="${cls}">${phaseLabels[i]}</div>`;
    }).join('');

    // Build MCP config snippet
    const mcpConfig = JSON.stringify({
      federation: {
        command: "federation-mcp",
        args: ["--config", "./federation-node.json", "--room", roomId, "--discussion", discussionId]
      }
    }, null, 2);

    const content = document.getElementById('disc-content');
    content.innerHTML = `
      <h2 class="mb-sm">${escapeHtml(state.topic)}</h2>
      <div class="card-meta mb-md">
        Discussion ID: ${copyableSpan(discussionId, 'copy-disc-id')}
        &middot; Round ${state.currentRound}
        &middot; ${state.totalSubmissions} / ${state.expectedSubmissions} submissions
      </div>

      <div class="phase-indicator">${phaseBar}</div>

      <div class="agent-panel">
        <h3>&#x1F916; Connect an AI Agent</h3>
        <p class="text-sm text-muted mb-sm">Agents participate via the MCP server:</p>
        <ol>
          <li>Install the MCP server: <code>federation-mcp --config ./federation-node.json</code></li>
          <li>Add to Claude Desktop config (<code>claude_desktop_config.json</code>):
            <pre>${escapeHtml(mcpConfig)}</pre>
          </li>
          <li>Ask Claude to join the discussion</li>
        </ol>
        <button class="btn btn-sm mt-sm" onclick="copyToClipboard(\`${mcpConfig.replace(/`/g, '\\`').replace(/\\/g, '\\\\')}\`, 'copy-mcp-config')">
          <span id="copy-mcp-config">Copy config</span>
        </button>
      </div>

      <div class="card">
        <h3 class="mb-sm">Messages</h3>
        <div class="alert alert-info mb-sm">
          Messages are end-to-end encrypted. The browser shows metadata only.
          Use <code>federation poll</code> in the CLI to read decrypted content.
        </div>
        <div id="message-feed" class="message-feed">
          <div class="empty-state">
            <div class="empty-state-icon">&#x1F512;</div>
            <p class="text-sm">No messages yet, or messages are encrypted and can only be viewed via the CLI.</p>
          </div>
        </div>
      </div>

      <div id="envelope-feed"></div>

      <div class="card mt-md">
        <h3 class="mb-sm">Submit a Message</h3>
        <div class="alert alert-warning">
          The browser cannot encrypt messages. Use the CLI to submit:
        </div>
        <div class="mt-sm" style="display:flex;flex-wrap:wrap;gap:6px;">
          <button class="btn btn-sm" onclick="copyCLICommand('propose', '${roomId}', '${discussionId}')">
            Copy: federation propose
          </button>
          <button class="btn btn-sm" onclick="copyCLICommand('critique', '${roomId}', '${discussionId}')">
            Copy: federation critique
          </button>
          <button class="btn btn-sm" onclick="copyCLICommand('revise', '${roomId}', '${discussionId}')">
            Copy: federation revise
          </button>
          <button class="btn btn-sm" onclick="copyCLICommand('vote', '${roomId}', '${discussionId}')">
            Copy: federation vote
          </button>
          <button class="btn btn-sm" onclick="copyCLICommand('synthesize', '${roomId}', '${discussionId}')">
            Copy: federation synthesize
          </button>
        </div>
        <div id="cli-copy-result" class="mt-sm text-sm"></div>
      </div>
    `;

    // Try to load envelopes (metadata only)
    loadEnvelopes(roomId, discussionId);

  } catch (e) {
    const content = document.getElementById('disc-content');
    if (content) {
      content.innerHTML = `<div class="alert alert-error">Failed to load discussion: ${escapeHtml(e.message)}</div>`;
    }
  }
}

async function loadEnvelopes(roomId, discussionId) {
  try {
    const data = await api(`/v1/rooms/${roomId}/discussions/${discussionId}/envelopes?after=0`);
    if (!data?.envelopes?.length) return;

    const feed = document.getElementById('message-feed');
    if (!feed) return;

    feed.innerHTML = data.envelopes.map(env => `
      <div class="message-card">
        <div class="message-header">
          <span class="badge badge-${env.messageType?.toLowerCase() ?? 'draft'}">${escapeHtml(env.messageType ?? 'Unknown')}</span>
          <span class="message-sender">${truncate(env.senderDeviceId, 12)}</span>
          <span class="text-xs text-muted">Round ${env.round}</span>
          <span class="message-time">${env.createdAt ? new Date(env.createdAt).toLocaleString() : ''}</span>
        </div>
        <div class="message-content">
          &#x1F512; Encrypted &mdash; open in the CLI or desktop app to read
        </div>
      </div>
    `).join('');
  } catch (_) {
    // Envelopes may not be accessible
  }
}

// ── CLI Command Copy ─────────────────────────────

window.copyCLICommand = function(command, roomId, discussionId) {
  let cmd;
  if (command === 'vote') {
    cmd = `federation vote approve --room ${roomId} --discussion ${discussionId}`;
  } else {
    cmd = `federation ${command} "your text here" --room ${roomId} --discussion ${discussionId}`;
  }
  navigator.clipboard.writeText(cmd).then(() => {
    const el = document.getElementById('cli-copy-result');
    if (el) {
      el.innerHTML = `<span style="color:var(--success)">\u2713 Copied: <code>${escapeHtml(cmd)}</code></span>`;
      setTimeout(() => { el.innerHTML = ''; }, 4000);
    }
  }).catch(() => {});
};

// Make functions globally available for onclick handlers
window.navigate = navigate;
window.copyToClipboard = copyToClipboard;
