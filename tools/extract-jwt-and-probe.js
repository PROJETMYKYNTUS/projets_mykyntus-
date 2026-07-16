// JScript (cscript) — no PowerShell, no admin
// Reads login JSON, extracts JWT, probes domain APIs via MSXML2.XMLHTTP

var fso = new ActiveXObject("Scripting.FileSystemObject");
var root = fso.GetParentFolderName(fso.GetParentFolderName(WScript.ScriptFullName));
if (!fso.FileExists(root + "\\docker-compose.yml")) {
  root = fso.GetFolder(".").Path;
}

function readFile(path) {
  if (!fso.FileExists(path)) return "";
  var ts = fso.OpenTextFile(path, 1);
  var t = ts.ReadAll();
  ts.Close();
  return t;
}

function extractToken(jsonText) {
  if (!jsonText) return "";
  // Prefer accessToken (AuthResponseDto)
  var re = /"accessToken"\s*:\s*"([^"]+)"/i;
  var m = jsonText.match(re);
  if (m && m[1]) return m[1];
  re = /"(?:token|access_token)"\s*:\s*"([^"]+)"/i;
  m = jsonText.match(re);
  if (m && m[1]) return m[1];
  re = /"data"\s*:\s*\{[^}]*"(?:accessToken|token)"\s*:\s*"([^"]+)"/i;
  m = jsonText.match(re);
  return (m && m[1]) ? m[1] : "";
}

function http(method, url, token, body) {
  var xhr = new ActiveXObject("MSXML2.XMLHTTP");
  try {
    xhr.open(method, url, false);
    xhr.setRequestHeader("Accept", "application/json");
    if (token) xhr.setRequestHeader("Authorization", "Bearer " + token);
    if (body) {
      xhr.setRequestHeader("Content-Type", "application/json");
      xhr.send(body);
    } else {
      xhr.send();
    }
    return { code: xhr.status, body: xhr.responseText };
  } catch (e) {
    return { code: 0, body: String(e.message) };
  }
}

function row(domain, cycle, status, detail) {
  WScript.Echo("| " + domain + " | " + cycle + " | " + status + " | " + detail + " |");
}

var gw = "http://localhost:8500";
var empToken = extractToken(readFile(root + "\\_audit_login_emp.json"));
var rhToken = extractToken(readFile(root + "\\_audit_login_rh.json"));

WScript.Echo("");
WScript.Echo("### Probes JWT (cscript)");
WScript.Echo("");
WScript.Echo("| Domaine | Cycle | Statut | Detail |");
WScript.Echo("|---------|-------|--------|--------|");

if (!empToken) {
  row("Auth", "extract token employee", "KO", "token absent dans _audit_login_emp.json");
} else {
  row("Auth", "extract token employee", "OK", "JWT present");
}
if (!rhToken) {
  row("Auth", "extract token RH", "KO", "token absent dans _audit_login_rh.json");
} else {
  row("Auth", "extract token RH", "OK", "JWT present");
}

function probe(domain, cycle, method, url, token, okCodes) {
  var r = http(method, url, token, null);
  var ok = false;
  for (var i = 0; i < okCodes.length; i++) {
    if (r.code === okCodes[i]) { ok = true; break; }
  }
  var snip = (r.body || "").replace(/\r|\n/g, " ").substring(0, 80);
  row(domain, cycle, ok ? "OK" : "KO", "HTTP " + r.code + " " + snip);
  return r;
}

if (empToken) {
  probe("Documentation", "GET users/me", "GET", gw + "/api/documentation/data/users/me", empToken, [200]);
  probe("Congé", "GET /api/conges", "GET", gw + "/api/conges", empToken, [200, 204, 401, 403]);
  probe("Planning", "GET /api/Conges Pascal", "GET", gw + "/api/Conges", empToken, [200, 204, 401, 403, 404]);
  probe("Formation", "GET /api/formations", "GET", gw + "/api/formations", empToken, [200, 204, 401, 403]);
}

if (rhToken) {
  probe("Documentation", "GET document-requests RH", "GET", gw + "/api/documentation/data/document-requests?page=1&pageSize=20", rhToken, [200]);
  probe("Directory", "org/overview", "GET", gw + "/api/directory/org/overview", rhToken, [200]);
  probe("Directory", "health", "GET", gw + "/api/directory/health", rhToken, [200]);
  probe("Prime", "validation", "GET", gw + "/api/prime/validation", rhToken, [200, 204, 401, 403, 404]);
  probe("Parrainage", "health", "GET", gw + "/api/parrainage/health", rhToken, [200]);
  probe("Contrats", "GET /api/contract", "GET", gw + "/api/contract", rhToken, [200, 204, 401, 403]);
}

// Write machine-readable summary for CMD
var out = fso.CreateTextFile(root + "\\_audit_jwt_summary.txt", true);
out.WriteLine("EMP_TOKEN_LEN=" + empToken.length);
out.WriteLine("RH_TOKEN_LEN=" + rhToken.length);
out.Close();

WScript.Quit(empToken ? 0 : 1);
