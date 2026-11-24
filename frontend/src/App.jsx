import React, { useState, useEffect, useRef, useCallback } from 'react';
import * as signalR from '@microsoft/signalr';
import {
  Monitor,
  Activity,
  ShieldCheck,
  ShieldAlert,
  Server,
  RefreshCw,
  Power,
  Trash2,
  CheckCircle,
  XCircle,
  Terminal,
  Cpu,
  HardDrive,
  Wifi,
  WifiOff,
  Zap,
  Play
} from 'lucide-react';

export default function App() {
  const [apiBaseUrl, setApiBaseUrl] = useState(() => localStorage.getItem('gcms_api_url') || 'http://localhost:5022');
  const [adminToken, setAdminToken] = useState(() => localStorage.getItem('gcms_admin_token') || '');
  const [activeTab, setActiveTab] = useState('stations');

  // Health and connection state
  const [apiConnected, setApiConnected] = useState(false);
  const [checkingApi, setCheckingApi] = useState(false);
  const [streamConnected, setStreamConnected] = useState(false);

  // Stations & Live logs
  const [devices, setDevices] = useState([]);
  const [pendingDevices, setPendingDevices] = useState([]);
  const [loadingDevices, setLoadingDevices] = useState(false);
  const [liveLogs, setLiveLogs] = useState([]);

  // Simulator state
  const [simDevice, setSimDevice] = useState(null);
  const [simToken, setSimToken] = useState(null);
  const [simHeartbeatActive, setSimHeartbeatActive] = useState(false);
  const [simSeq, setSimSeq] = useState(1);
  const simConnRef = useRef(null);

  useEffect(() => {
    localStorage.setItem('gcms_api_url', apiBaseUrl);
  }, [apiBaseUrl]);

  useEffect(() => {
    localStorage.setItem('gcms_admin_token', adminToken);
  }, [adminToken]);

  const addLog = useCallback((source, type, message, meta = null) => {
    setLiveLogs(prev => [
      {
        id: Math.random().toString(36).substring(2, 9),
        time: new Date().toLocaleTimeString(),
        source,
        type, // 'info' | 'success' | 'warn' | 'error'
        message,
        meta
      },
      ...prev.slice(0, 199)
    ]);
  }, []);

  // Ping Backend
  const pingBackend = useCallback(async (customUrl = apiBaseUrl) => {
    setCheckingApi(true);
    try {
      const res = await fetch(`${customUrl}/weatherforecast`);
      if (res.ok) {
        setApiConnected(true);
        addLog('API', 'success', `Connected to backend at ${customUrl}`);
      } else {
        setApiConnected(false);
      }
    } catch {
      setApiConnected(false);
    } finally {
      setCheckingApi(false);
    }
  }, [apiBaseUrl, addLog]);

  // Fetch Devices
  const fetchDevices = useCallback(async () => {
    if (!adminToken) return;
    setLoadingDevices(true);
    try {
      const headers = {
        'Accept': 'application/json',
        'Authorization': `Bearer ${adminToken}`
      };

      const devRes = await fetch(`${apiBaseUrl}/api/devices`, { headers });
      if (devRes.ok) {
        const data = await devRes.json();
        setDevices(data);
      }

      const pendRes = await fetch(`${apiBaseUrl}/api/devices/pending`, { headers });
      if (pendRes.ok) {
        const pending = await pendRes.json();
        setPendingDevices(pending);
      }
    } catch (err) {
      addLog('API', 'error', `Failed to query devices: ${err.message}`);
    } finally {
      setLoadingDevices(false);
    }
  }, [apiBaseUrl, adminToken, addLog]);

  // Instant Dev Login
  const handleQuickDevLogin = async () => {
    try {
      addLog('AUTH', 'info', 'Acquiring Dev Admin Token...');
      const res = await fetch(`${apiBaseUrl}/api/auth/dev-token`, { method: 'POST' });
      if (res.ok) {
        const data = await res.json();
        setAdminToken(data.accessToken);
        addLog('AUTH', 'success', 'Dev Admin authenticated! Live SignalR stream starting...');
      } else {
        addLog('AUTH', 'error', `Dev token error: HTTP ${res.status}`);
      }
    } catch (err) {
      addLog('AUTH', 'error', `Dev login failed: ${err.message}`);
    }
  };

  useEffect(() => {
    pingBackend();
  }, [pingBackend]);

  useEffect(() => {
    if (!apiConnected || !adminToken) return;
    fetchDevices();
    const interval = setInterval(fetchDevices, 8000);
    return () => clearInterval(interval);
  }, [apiConnected, adminToken, fetchDevices]);

  // Persistent SignalR Live Telemetry Stream
  useEffect(() => {
    if (!adminToken) {
      setStreamConnected(false);
      return;
    }

    const hubUrl = `${apiBaseUrl}/hubs/agent`;
    const conn = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, {
        accessTokenFactory: () => adminToken,
        skipNegotiation: false
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    conn.on('HeartbeatReceived', (data) => {
      const name = data.deviceCode || data.deviceId?.substring(0, 8);
      addLog('HEARTBEAT', 'info', `💓 Station ${name} | CPU: ${data.cpuPercent}% | RAM: ${data.memoryUsageMb} MB`, data);

      // Instantly update station in React state
      setDevices(prev => prev.map(d => {
        if (d.id === data.deviceId) {
          return {
            ...d,
            status: {
              ...d.status,
              lastSeenAt: data.timestamp,
              reportedPowerState: 'ON',
              lastIpAddress: data.ip || d.status?.lastIpAddress
            },
            liveCpu: data.cpuPercent,
            liveMem: data.memoryUsageMb
          };
        }
        return d;
      }));
    });

    conn.on('AgentConnected', (data) => {
      const name = data.deviceCode || data.deviceId?.substring(0, 8);
      addLog('STREAM', 'success', `🟢 Station ${name} connected to Hub`, data);
      setDevices(prev => prev.map(d => d.id === data.deviceId ? {
        ...d,
        status: { ...d.status, reportedPowerState: 'ON', lastSeenAt: data.timestamp, lastIpAddress: data.ip }
      } : d));
    });

    conn.on('AgentDisconnected', (data) => {
      const name = data.deviceId?.substring(0, 8);
      addLog('STREAM', 'warn', `🔴 Station ${name} disconnected (${data.reason})`, data);
      setDevices(prev => prev.map(d => d.id === data.deviceId ? {
        ...d,
        status: { ...d.status, reportedPowerState: 'OFF' }
      } : d));
    });

    conn.on('DeviceRegistered', (data) => {
      addLog('STREAM', 'info', `✨ New Station Registered: ${data.deviceCode} (${data.displayName})`);
      fetchDevices();
    });

    conn.on('DeviceApproved', (data) => {
      addLog('STREAM', 'success', `✅ Device ${data.deviceCode} approved!`);
      fetchDevices();
    });

    conn.on('DeviceRevoked', (data) => {
      addLog('STREAM', 'warn', `⚠️ Device ${data.deviceCode} revoked.`);
      fetchDevices();
    });

    conn.on('DeviceReauthorized', (data) => {
      addLog('STREAM', 'success', `⚡ Device ${data.deviceCode} re-authorized!`);
      fetchDevices();
    });

    conn.on('DeviceDeleted', (data) => {
      addLog('STREAM', 'error', `🗑️ Device ${data.deviceCode} erased from database.`);
      fetchDevices();
    });

    conn.start()
      .then(() => {
        setStreamConnected(true);
        addLog('SIGNALR', 'success', '⚡ Live SignalR Dashboard Stream connected!');
      })
      .catch(err => {
        setStreamConnected(false);
        addLog('SIGNALR', 'error', `Live stream connection error: ${err.message}`);
      });

    conn.onreconnecting(() => setStreamConnected(false));
    conn.onreconnected(() => {
      setStreamConnected(true);
      fetchDevices();
    });
    conn.onclose(() => setStreamConnected(false));

    return () => {
      conn.stop();
    };
  }, [adminToken, apiBaseUrl, fetchDevices, addLog]);

  // Actions
  const handleApproveDevice = async (id, code) => {
    try {
      addLog('ACTION', 'info', `Approving ${code}...`);
      const res = await fetch(`${apiBaseUrl}/api/devices/${id}/approve`, {
        method: 'POST',
        headers: { 'Authorization': `Bearer ${adminToken}`, 'Content-Type': 'application/json' }
      });
      if (res.ok) {
        addLog('ACTION', 'success', `Station ${code} APPROVED!`);
        fetchDevices();
      }
    } catch (err) {
      addLog('ACTION', 'error', `Approval error: ${err.message}`);
    }
  };

  const handleRejectDevice = async (id, code) => {
    try {
      addLog('ACTION', 'warn', `Rejecting ${code}...`);
      const res = await fetch(`${apiBaseUrl}/api/devices/${id}/reject`, {
        method: 'POST',
        headers: { 'Authorization': `Bearer ${adminToken}`, 'Content-Type': 'application/json' }
      });
      if (res.ok) fetchDevices();
    } catch (err) {
      addLog('ACTION', 'error', `Reject error: ${err.message}`);
    }
  };

  const handleRevokeDevice = async (id, code) => {
    if (!confirm(`Revoke station ${code}? The agent will immediately disconnect and wipe credentials.`)) return;
    try {
      addLog('ACTION', 'warn', `Revoking ${code}...`);
      const res = await fetch(`${apiBaseUrl}/api/devices/${id}/revoke`, {
        method: 'POST',
        headers: { 'Authorization': `Bearer ${adminToken}`, 'Content-Type': 'application/json' }
      });
      if (res.ok) fetchDevices();
    } catch (err) {
      addLog('ACTION', 'error', `Revoke error: ${err.message}`);
    }
  };

  const handleReauthorizeDevice = async (id, code) => {
    try {
      addLog('ACTION', 'info', `Re-authorizing ${code}...`);
      const res = await fetch(`${apiBaseUrl}/api/devices/${id}/reauthorize`, {
        method: 'POST',
        headers: { 'Authorization': `Bearer ${adminToken}`, 'Content-Type': 'application/json' }
      });
      if (res.ok) fetchDevices();
    } catch (err) {
      addLog('ACTION', 'error', `Reauthorize error: ${err.message}`);
    }
  };

  const handleDeleteDevice = async (id, code) => {
    if (!confirm(`COMPLETELY ERASE station ${code} from database?\n\nThe agent will reset its identity and submit a fresh registration for you to approve again.`)) return;
    try {
      addLog('ACTION', 'warn', `Erasing ${code} completely from database...`);
      const res = await fetch(`${apiBaseUrl}/api/devices/${id}`, {
        method: 'DELETE',
        headers: { 'Authorization': `Bearer ${adminToken}`, 'Content-Type': 'application/json' }
      });
      if (res.ok) {
        addLog('ACTION', 'success', `Station ${code} ERASED! Watch for it to appear in 'Pending Approvals' shortly.`);
        fetchDevices();
      }
    } catch (err) {
      addLog('ACTION', 'error', `Delete error: ${err.message}`);
    }
  };

  return (
    <div className="flex flex-col min-h-screen bg-slate-950 text-slate-100">
      {/* Top Header */}
      <header className="glass sticky top-0 z-50 px-6 py-3 border-b border-slate-800">
        <div className="max-w-7xl mx-auto flex flex-wrap items-center justify-between gap-4">
          <div className="flex items-center gap-3">
            <div className="w-10 h-10 rounded-xl bg-gradient-to-br from-cyan-400 to-blue-600 flex items-center justify-center shadow-lg shadow-cyan-500/20 font-black text-slate-950 text-lg">
              GC
            </div>
            <div>
              <div className="flex items-center gap-2">
                <h1 className="font-bold text-lg text-slate-100 tracking-wide">Gaming Cafe Console</h1>
                <span className="text-[10px] px-2 py-0.5 rounded-full bg-cyan-950 border border-cyan-800 text-cyan-300 font-mono">React 18 + SignalR</span>
              </div>
              <p className="text-xs text-slate-400">Real-Time Telemetry & Zero-Touch Management</p>
            </div>
          </div>

          <div className="flex items-center gap-3">
            <div className="flex items-center rounded-lg bg-slate-900 border border-slate-800 p-1">
              <span className="text-xs text-slate-400 px-2 font-mono">API:</span>
              <input
                type="text"
                value={apiBaseUrl}
                onChange={e => setApiBaseUrl(e.target.value)}
                className="bg-transparent text-xs text-slate-200 font-mono focus:outline-none w-44 px-1"
              />
              <button
                onClick={() => pingBackend(apiBaseUrl)}
                disabled={checkingApi}
                className="px-2 py-1 rounded bg-slate-800 hover:bg-slate-700 text-xs font-medium text-slate-300 transition"
              >
                {checkingApi ? '...' : 'Ping'}
              </button>
            </div>

            <div className="flex items-center gap-2 px-3 py-1.5 rounded-lg border border-slate-800 bg-slate-900/60 text-xs font-mono">
              <span className={`w-2.5 h-2.5 rounded-full ${apiConnected ? 'bg-emerald-400 shadow-md shadow-emerald-500/50' : 'bg-rose-500'}`}></span>
              <span className={apiConnected ? 'text-emerald-400' : 'text-rose-400'}>
                {apiConnected ? 'API Online' : 'API Offline'}
              </span>
            </div>

            <div className="flex items-center gap-2 px-3 py-1.5 rounded-lg border border-slate-800 bg-slate-900/60 text-xs font-mono">
              <span className={`w-2.5 h-2.5 rounded-full ${streamConnected ? 'bg-cyan-400 shadow-md shadow-cyan-500/50 animate-pulse' : 'bg-slate-600'}`}></span>
              <span className={streamConnected ? 'text-cyan-300 font-medium' : 'text-slate-400'}>
                {streamConnected ? 'Live Stream Active' : 'Stream Standby'}
              </span>
            </div>

            {!adminToken ? (
              <button
                onClick={handleQuickDevLogin}
                className="px-3.5 py-1.5 rounded-lg bg-gradient-to-r from-amber-500 to-orange-500 hover:from-amber-400 hover:to-orange-400 text-slate-950 font-bold text-xs flex items-center gap-1.5 shadow-md shadow-amber-500/20 transition"
              >
                <Zap size={14} /> Instant Dev Login
              </button>
            ) : (
              <div className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-emerald-950/80 border border-emerald-800 text-emerald-400 text-xs font-mono">
                <ShieldCheck size={14} />
                <span className="font-semibold">Admin Active</span>
                <button onClick={() => setAdminToken('')} className="ml-1 text-slate-400 hover:text-rose-400 text-xs">✕</button>
              </div>
            )}
          </div>
        </div>
      </header>

      {/* Main Content */}
      <main className="flex-1 max-w-7xl w-full mx-auto p-6 flex flex-col gap-6">
        {/* KPI Cards */}
        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-5 gap-4">
          <div className="glass p-5 rounded-2xl border border-slate-800/80">
            <div className="text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1">Total Stations</div>
            <div className="text-3xl font-extrabold text-slate-100 font-mono">{devices.length}</div>
            <div className="text-xs text-slate-500 mt-2">Enrolled machines</div>
          </div>

          <div className="glass p-5 rounded-2xl border border-slate-800/80">
            <div className="text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1">Approved Stations</div>
            <div className="text-3xl font-extrabold text-emerald-400 font-mono">
              {devices.filter(d => d.registrationStatus === 'APPROVED').length}
            </div>
            <div className="text-xs text-emerald-500/80 mt-2">Active cafe stations</div>
          </div>

          <div className="glass p-5 rounded-2xl border border-slate-800/80">
            <div className="text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1">Pending Approval</div>
            <div className="text-3xl font-extrabold text-amber-400 font-mono">{pendingDevices.length}</div>
            <div className="text-xs text-amber-500/80 mt-2">Needs admin approval</div>
          </div>

          <div className="glass p-5 rounded-2xl border border-slate-800/80">
            <div className="text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1">Revoked Stations</div>
            <div className="text-3xl font-extrabold text-rose-400 font-mono">
              {devices.filter(d => d.registrationStatus === 'REVOKED').length}
            </div>
            <div className="text-xs text-rose-500/80 mt-2">Halted machines</div>
          </div>

          <div className="glass p-5 rounded-2xl border border-slate-800/80">
            <div className="text-xs font-semibold text-slate-400 uppercase tracking-wider mb-1">Telemetry Stream</div>
            <div className="text-3xl font-extrabold text-cyan-400 font-mono">{liveLogs.length}</div>
            <div className="text-xs text-cyan-500/80 mt-2">Events captured</div>
          </div>
        </div>

        {/* Tab Selector */}
        <div className="flex border-b border-slate-800 gap-2">
          {[
            { id: 'stations', label: 'Stations Monitor', count: devices.length },
            { id: 'pending', label: 'Pending Approvals', count: pendingDevices.length, highlight: pendingDevices.length > 0 },
            { id: 'telemetry', label: 'Live SignalR Telemetry', count: liveLogs.length }
          ].map(tab => (
            <button
              key={tab.id}
              onClick={() => setActiveTab(tab.id)}
              className={`px-4 py-2.5 font-medium text-sm rounded-t-xl transition relative flex items-center gap-2 ${
                activeTab === tab.id
                  ? 'bg-slate-900 border-t border-x border-slate-800 text-cyan-400'
                  : 'text-slate-400 hover:text-slate-200 hover:bg-slate-900/40'
              }`}
            >
              {tab.label}
              <span className={`text-xs px-2 py-0.5 rounded-full font-mono ${
                tab.highlight ? 'bg-amber-500/20 text-amber-300 font-bold' : 'bg-slate-800 text-slate-400'
              }`}>
                {tab.count}
              </span>
            </button>
          ))}
        </div>

        {/* TAB 1: STATIONS MONITOR */}
        {activeTab === 'stations' && (
          <div className="flex flex-col gap-4">
            <div className="flex items-center justify-between">
              <div>
                <h2 className="text-lg font-bold text-slate-100 flex items-center gap-2">
                  <Monitor size={20} className="text-cyan-400" /> Real-Time Stations Monitor
                </h2>
                <p className="text-xs text-slate-400">Everything refreshes automatically via live WebSocket telemetry</p>
              </div>
              <button
                onClick={fetchDevices}
                disabled={loadingDevices}
                className="px-3 py-1.5 rounded-lg bg-slate-900 hover:bg-slate-800 border border-slate-700 text-xs font-mono text-slate-200 flex items-center gap-2 transition"
              >
                <RefreshCw size={12} className={loadingDevices ? 'animate-spin' : ''} /> Refresh
              </button>
            </div>

            {devices.length === 0 ? (
              <div className="glass p-12 rounded-2xl text-center border border-slate-800/80 flex flex-col items-center">
                <Monitor size={48} className="text-slate-600 mb-3" />
                <div className="text-slate-300 font-medium">No stations registered yet</div>
                <p className="text-slate-500 text-xs mt-1">Start the Client Agent in Terminal 2 to automatically enroll.</p>
              </div>
            ) : (
              <div className="glass rounded-2xl border border-slate-800/80 overflow-hidden">
                <table className="w-full text-left text-sm">
                  <thead className="bg-slate-900/80 text-xs text-slate-400 uppercase tracking-wider font-mono border-b border-slate-800">
                    <tr>
                      <th className="py-3.5 px-4">Station / Hostname</th>
                      <th className="py-3.5 px-4">Device Code</th>
                      <th className="py-3.5 px-4">Status</th>
                      <th className="py-3.5 px-4">Live Resources</th>
                      <th className="py-3.5 px-4">IP Address</th>
                      <th className="py-3.5 px-4">Last Telemetry</th>
                      <th className="py-3.5 px-4 text-right">Actions</th>
                    </tr>
                  </thead>
                  <tbody className="divide-y divide-slate-800/60 font-mono text-xs">
                    {devices.map(dev => {
                      const isApproved = dev.registrationStatus === 'APPROVED';
                      const isPending = dev.registrationStatus === 'PENDING';
                      const isRevoked = dev.registrationStatus === 'REVOKED';
                      const status = dev.status;

                      return (
                        <tr key={dev.id} className="hover:bg-slate-900/50 transition">
                          <td className="py-3.5 px-4">
                            <div className="font-semibold text-slate-200 font-sans text-sm">{dev.displayName || dev.deviceCode}</div>
                            <div className="text-slate-500 text-xs">{dev.hostname} (v{dev.agentVersion || '1.0'})</div>
                          </td>
                          <td className="py-3.5 px-4 text-cyan-400 font-bold">{dev.deviceCode}</td>
                          <td className="py-3.5 px-4">
                            {isApproved ? (
                              <span className="px-2.5 py-1 rounded-full bg-emerald-950 text-emerald-400 border border-emerald-800 text-[11px] font-semibold flex items-center gap-1.5 w-max">
                                <span className="w-1.5 h-1.5 rounded-full bg-emerald-400"></span> APPROVED
                              </span>
                            ) : isPending ? (
                              <span className="px-2.5 py-1 rounded-full bg-amber-950 text-amber-400 border border-amber-800 text-[11px] font-semibold flex items-center gap-1.5 w-max">
                                <span className="w-1.5 h-1.5 rounded-full bg-amber-400 animate-ping"></span> PENDING
                              </span>
                            ) : isRevoked ? (
                              <span className="px-2.5 py-1 rounded-full bg-rose-950 text-rose-400 border border-rose-800 text-[11px] font-semibold flex items-center gap-1.5 w-max">
                                <span className="w-1.5 h-1.5 rounded-full bg-rose-400"></span> REVOKED
                              </span>
                            ) : (
                              <span className="px-2.5 py-1 rounded-full bg-slate-800 text-slate-300 border border-slate-700 text-[11px] font-semibold w-max">
                                {dev.registrationStatus}
                              </span>
                            )}
                          </td>
                          <td className="py-3.5 px-4">
                            <div className="flex items-center gap-2">
                              {dev.liveCpu !== undefined ? (
                                <span className="text-[11px] bg-cyan-950/80 text-cyan-300 px-2 py-0.5 rounded font-mono border border-cyan-800/60 font-semibold flex items-center gap-1">
                                  <Cpu size={11} /> {dev.liveCpu}%
                                </span>
                              ) : (
                                <span className="text-slate-500 text-[11px]">-</span>
                              )}
                              {dev.liveMem !== undefined ? (
                                <span className="text-[11px] bg-emerald-950/80 text-emerald-300 px-2 py-0.5 rounded font-mono border border-emerald-800/60 font-semibold flex items-center gap-1">
                                  <HardDrive size={11} /> {dev.liveMem} MB
                                </span>
                              ) : null}
                            </div>
                          </td>
                          <td className="py-3.5 px-4 text-slate-400">{status?.lastIpAddress || '-'}</td>
                          <td className="py-3.5 px-4 text-slate-400">
                            <div className="flex items-center gap-1.5">
                              {status?.lastSeenAt && <span className="w-1.5 h-1.5 rounded-full bg-emerald-400 animate-ping"></span>}
                              <span>{status?.lastSeenAt ? new Date(status.lastSeenAt).toLocaleTimeString() : 'Never'}</span>
                            </div>
                          </td>
                          <td className="py-3.5 px-4 text-right">
                            <div className="flex items-center justify-end gap-1.5">
                              {isPending ? (
                                <>
                                  <button
                                    onClick={() => handleApproveDevice(dev.id, dev.deviceCode)}
                                    className="px-2.5 py-1 bg-emerald-600 hover:bg-emerald-500 text-white rounded font-sans font-medium text-xs transition"
                                  >
                                    Approve
                                  </button>
                                  <button
                                    onClick={() => handleRejectDevice(dev.id, dev.deviceCode)}
                                    className="px-2 py-1 bg-slate-800 hover:bg-rose-950 hover:text-rose-400 text-slate-400 rounded font-sans text-xs transition"
                                  >
                                    Reject
                                  </button>
                                </>
                              ) : isApproved ? (
                                <button
                                  onClick={() => handleRevokeDevice(dev.id, dev.deviceCode)}
                                  className="px-2.5 py-1 bg-rose-950/80 hover:bg-rose-900 border border-rose-800 text-rose-300 hover:text-rose-100 rounded font-sans font-medium text-xs transition"
                                >
                                  Revoke
                                </button>
                              ) : (
                                <button
                                  onClick={() => handleReauthorizeDevice(dev.id, dev.deviceCode)}
                                  className="px-2.5 py-1 bg-cyan-600 hover:bg-cyan-500 text-white rounded font-sans font-medium text-xs transition flex items-center gap-1"
                                >
                                  <Zap size={11} /> Re-Authorize
                                </button>
                              )}
                              <button
                                onClick={() => handleDeleteDevice(dev.id, dev.deviceCode)}
                                className="p-1.5 text-slate-500 hover:text-rose-400 hover:bg-rose-950/40 rounded transition"
                                title="Erase completely from database"
                              >
                                <Trash2 size={13} />
                              </button>
                            </div>
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        )}

        {/* TAB 2: PENDING APPROVALS */}
        {activeTab === 'pending' && (
          <div className="flex flex-col gap-4">
            <div>
              <h2 className="text-lg font-bold text-slate-100 flex items-center gap-2">
                <ShieldAlert size={20} className="text-amber-400" /> Pending Device Approvals
              </h2>
              <p className="text-xs text-slate-400">Newly connected gaming PCs awaiting authorization</p>
            </div>

            {pendingDevices.length === 0 ? (
              <div className="glass p-12 rounded-2xl text-center border border-slate-800/80 flex flex-col items-center">
                <CheckCircle size={48} className="text-emerald-500 mb-3" />
                <div className="text-slate-200 font-semibold text-base">All Stations Approved</div>
                <p className="text-slate-500 text-xs mt-1">No gaming PCs are currently waiting for admin approval.</p>
              </div>
            ) : (
              <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
                {pendingDevices.map(dev => (
                  <div key={dev.id} className="glass p-5 rounded-2xl border border-amber-800/60 bg-gradient-to-b from-slate-900 to-amber-950/20 flex flex-col justify-between gap-4">
                    <div>
                      <div className="flex items-center justify-between">
                        <span className="text-xs font-mono px-2 py-0.5 rounded bg-amber-500/20 text-amber-300 font-bold border border-amber-500/30">
                          {dev.deviceCode}
                        </span>
                        <span className="text-[10px] text-slate-400 font-mono">
                          {new Date(dev.createdAt).toLocaleTimeString()}
                        </span>
                      </div>
                      <div className="text-lg font-bold text-slate-100 mt-2">{dev.displayName}</div>
                      <div className="text-xs text-slate-400 mt-0.5 font-mono">Host: {dev.hostname}</div>
                    </div>

                    <div className="flex gap-2">
                      <button
                        onClick={() => handleApproveDevice(dev.id, dev.deviceCode)}
                        className="flex-1 py-2 bg-emerald-600 hover:bg-emerald-500 text-white rounded-xl font-bold text-xs transition shadow-md shadow-emerald-600/30 flex items-center justify-center gap-1.5"
                      >
                        <CheckCircle size={14} /> Approve Device
                      </button>
                      <button
                        onClick={() => handleRejectDevice(dev.id, dev.deviceCode)}
                        className="px-3 py-2 bg-slate-800 hover:bg-rose-950 hover:text-rose-400 text-slate-400 rounded-xl text-xs transition"
                      >
                        Reject
                      </button>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {/* TAB 3: LIVE SIGNALR TELEMETRY */}
        {activeTab === 'telemetry' && (
          <div className="flex flex-col gap-4">
            <div className="flex items-center justify-between">
              <div>
                <h2 className="text-lg font-bold text-slate-100 flex items-center gap-2">
                  <Activity size={20} className="text-cyan-400" /> Live SignalR Telemetry Stream
                </h2>
                <p className="text-xs text-slate-400">Real-time WebSocket event feed streaming directly from all gaming PC agents</p>
              </div>
              <button
                onClick={() => setLiveLogs([])}
                className="px-3 py-1.5 rounded-lg bg-slate-900 hover:bg-slate-800 border border-slate-700 text-xs font-mono text-slate-400 hover:text-slate-200 transition"
              >
                Clear Feed
              </button>
            </div>

            <div className="glass rounded-2xl border border-slate-800/80 p-4 font-mono text-xs flex flex-col gap-2 max-h-[550px] overflow-y-auto">
              {liveLogs.length === 0 ? (
                <div className="py-12 text-center text-slate-500 flex flex-col items-center">
                  <Activity size={36} className="text-slate-600 mb-2" />
                  <div>Listening for live telemetry...</div>
                  <div className="text-[11px] text-slate-600 mt-1">Start Terminal 2 (Client Agent) to see heartbeats stream live.</div>
                </div>
              ) : (
                liveLogs.map(log => (
                  <div key={log.id} className="flex items-start gap-3 py-1 px-2 rounded hover:bg-slate-900/60 transition border-b border-slate-900/40">
                    <span className="text-slate-500 whitespace-nowrap">{log.time}</span>
                    <span className={`px-1.5 py-0.2 rounded text-[10px] font-bold ${
                      log.source === 'HEARTBEAT' ? 'bg-cyan-500/20 text-cyan-300' :
                      log.source === 'CONNECTION' ? 'bg-emerald-500/20 text-emerald-300' :
                      log.source === 'SIGNALR' ? 'bg-purple-500/20 text-purple-300' :
                      log.source === 'ACTION' ? 'bg-amber-500/20 text-amber-300' :
                      'bg-slate-800 text-slate-300'
                    }`}>
                      {log.source}
                    </span>
                    <span className={`flex-1 ${
                      log.type === 'error' ? 'text-rose-400' :
                      log.type === 'warn' ? 'text-amber-300' :
                      log.type === 'success' ? 'text-emerald-400' :
                      'text-slate-300'
                    }`}>
                      {log.message}
                    </span>
                  </div>
                ))
              )}
            </div>
          </div>
        )}
      </main>
    </div>
  );
}
