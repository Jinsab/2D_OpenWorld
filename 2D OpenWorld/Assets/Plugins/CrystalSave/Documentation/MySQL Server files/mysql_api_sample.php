<?php
// ── CORS ─────────────────────────────────────────────────────────
header('Access-Control-Allow-Origin: *');  // change * to your domain in prod
header('Access-Control-Allow-Headers: Content-Type, X-API-KEY');
header('Access-Control-Allow-Methods: POST, GET, OPTIONS');
header('Access-Control-Expose-Headers: Content-Length');

if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') {
    // Pre-flight probe – no body, just confirm the CORS rules
    http_response_code(204);  // No Content
    exit;
}

// ── Config ───────────────────────────────────────────────────────
$db_host = '127.0.0.1';
$db_name = 'game';
$db_user = 'unityuser';
$db_pass = 'ChangeMe!';
$table   = 'CrystalSaveData';
$userTable = 'CrystalSaveUsers';
$apiKey  = 'SUPER_SECRET_TOKEN';   // blank = disable API-key check
$imgRoot = __DIR__ . '/img';      // store screenshots alongside this script

// ── Simple API-key gate ─────────────────────────────────────────
if ($apiKey !== '' && ($_SERVER['HTTP_X_API_KEY'] ?? '') !== $apiKey) {
    http_response_code(401);
    exit;
}

// ── DB connect ──────────────────────────────────────────────────
try {
    $pdo = new PDO(
        "mysql:host=$db_host;dbname=$db_name;charset=utf8mb4",
        $db_user, $db_pass,
        [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION]
    );
} catch (PDOException $e) {
    http_response_code(500);
    echo json_encode(['error' => 'DB connection failed']);
    exit;
}

// ── Routing ─────────────────────────────────────────────────────
// Accept action either as ?action=save or as /save via PATH_INFO
$action = $_GET['action'] ?? trim($_SERVER['PATH_INFO'] ?? '', '/');
$input  = json_decode(file_get_contents('php://input'), true);

// ── Upload image  (POST /uploadImage  multipart/form-data) ──────
if ($action === 'uploadImage') {
    // uid = user ID, slot = save slot, name = original filename
    if (!isset($_FILES['shot'], $_POST['uid'], $_POST['slot'], $_POST['name'])) {
        http_response_code(400); exit;
    }
    $uid  = $_POST['uid'];
    $slot = intval($_POST['slot']);
    // Keep the client-supplied name (timestamps etc.) but strip any path
    $targetName = basename($_POST['name']);
    $ext = strtolower(pathinfo($targetName, PATHINFO_EXTENSION));
    if ($ext === '') {
        $ext = strtolower(pathinfo($_FILES['shot']['name'], PATHINFO_EXTENSION)) ?: 'png';
        $targetName .= ".$ext";
    }
    $targetDir  = "$imgRoot/$uid";
    $targetPath = "$targetDir/$targetName";

    @mkdir($targetDir, 0750, true);
    if (move_uploaded_file($_FILES['shot']['tmp_name'], $targetPath)) {
        $stmt = $pdo->prepare(
            "UPDATE $table
             SET ScreenshotFileName=?, LastSavedTicks=UNIX_TIMESTAMP()*1000
             WHERE UserID=? AND Slot=?");
        $stmt->execute([$targetName, $uid, $slot]);
        exit; // 200 OK
    }
    http_response_code(500); exit;
}

// ── Read slot metadata  (GET /metadata?uid&slot) ────────────────
if ($action === 'metadata' && $_SERVER['REQUEST_METHOD'] === 'GET') {
    $uid  = $_GET['uid']  ?? '';
    $slot = $_GET['slot'] ?? '';
    if ($uid === '' || $slot === '') { http_response_code(400); exit; }

    $stmt = $pdo->prepare("
        SELECT Slot AS slot,
               COALESCE(SlotName, CONCAT('Slot ', Slot))  AS name,
               COALESCE(LastSavedTicks, 0)                AS ticks,
               COALESCE(LastActiveScene, '')             AS scene,
               COALESCE(ScreenshotFileName, '')          AS shot,
               COALESCE(CustomMetadata , '{}')           AS meta
          FROM $table
         WHERE UserID=? AND Slot=?");
    $stmt->execute([$uid, $slot]);

    if ($row = $stmt->fetch(PDO::FETCH_ASSOC)) {
        $obj         = json_decode($row['meta'], true) ?: [];
        $row['meta'] = array_map(fn($k,$v)=>['key'=>$k,'value'=>$v],
                                 array_keys($obj), $obj);
        echo json_encode($row);
    } else {
        http_response_code(404);
    }
    exit;
}

// ── JSON endpoints ──────────────────────────────────────────────
switch ($action) {

case 'signup': // {username,password}
    $username = $input['username'] ?? '';
    $password = $input['password'] ?? '';
    if ($username === '' || $password === '') { http_response_code(400); break; }
    try {
        $hash = password_hash($password, PASSWORD_DEFAULT);
        $pdo->prepare("INSERT INTO $userTable (Username,PasswordHash) VALUES (?,?)")
            ->execute([$username, $hash]);
    } catch (PDOException $e) {
        http_response_code(409);
    }
    break;

case 'login': // {username,password}
    $username = $input['username'] ?? '';
    $password = $input['password'] ?? '';
    if ($username === '' || $password === '') { http_response_code(400); break; }
    $stmt = $pdo->prepare("SELECT PasswordHash FROM $userTable WHERE Username=?");
    $stmt->execute([$username]);
    $hash = $stmt->fetchColumn();
    if ($hash && password_verify($password, $hash)) {
        echo json_encode(['uid' => $username]);
    } else {
        http_response_code(401);
    }
    break;

case 'save':      // {uid,slot,data}
    $uid  = $input['uid']  ?? '';
    $slot = $input['slot'] ?? '';
    $data = isset($input['data']) ? base64_decode($input['data']) : '';
    if ($uid === '' || $slot === '' || $data === '') { http_response_code(400); break; }

    $pdo->prepare("INSERT INTO $table (UserID,Slot,Data)
                   VALUES (?,?,?)
                   ON DUPLICATE KEY UPDATE Data=VALUES(Data)")
        ->execute([$uid,$slot,$data]);
    break;

case 'metadata':  // {uid,slot,name,ticks,scene,shot,meta}
    $uid   = $input['uid']   ?? '';
    $slot  = $input['slot']  ?? '';
    $name  = $input['name']  ?? '';
    $ticks = $input['ticks'] ?? '';
    $scene = $input['scene'] ?? '';
    $shot  = $input['shot']  ?? '';
    $metaJson = json_encode($input['meta'] ?? new stdClass());
    if ($uid === '' || $slot === '' || $name === '' || $ticks === '' || $scene === '' || $shot === '') { http_response_code(400); break; }

    $pdo->prepare("UPDATE $table
         SET SlotName=?, LastSavedTicks=?, LastActiveScene=?,
             ScreenshotFileName=?, CustomMetadata=?
       WHERE UserID=? AND Slot=?")
        ->execute([$name, $ticks, $scene, $shot, $metaJson, $uid, $slot]);
    break;

case 'load':      // GET ?uid&slot
    $stmt = $pdo->prepare(
      "SELECT Data FROM $table WHERE UserID=? AND Slot=?");
    $stmt->execute([$_GET['uid'], $_GET['slot']]);
    if ($row = $stmt->fetch(PDO::FETCH_NUM)) {
        echo base64_encode($row[0]);
    }
    break;

case 'delete':    // {uid,slot}
    $uid  = $input['uid']  ?? '';
    $slot = $input['slot'] ?? '';
    if ($uid === '' || $slot === '') { http_response_code(400); break; }

    // remove image first
    $img = $pdo->prepare(
      "SELECT ScreenshotFileName FROM $table WHERE UserID=? AND Slot=?");
    $img->execute([$uid, $slot]);
    if ($fn = $img->fetchColumn()) {
        @unlink("$imgRoot/$uid/$fn");
    }
    $pdo->prepare(
      "DELETE FROM $table WHERE UserID=? AND Slot=?")
        ->execute([$uid, $slot]);
    break;

case 'list':      // GET ?uid
    $stmt = $pdo->prepare(
      "SELECT Slot AS slot, SlotName AS name, LastSavedTicks AS ticks,
              LastActiveScene AS scene, ScreenshotFileName AS shot,
              CustomMetadata AS meta
         FROM $table
        WHERE UserID=?");
    $stmt->execute([$_GET['uid']]);
    $rows = $stmt->fetchAll(PDO::FETCH_ASSOC);
    foreach ($rows as &$r) {
        $obj       = json_decode($r['meta'], true) ?: [];
        $r['meta'] = array_map(fn($k,$v)=>['key'=>$k,'value'=>$v],
                               array_keys($obj), $obj);
    }
    echo json_encode($rows);
    break;

default:
    http_response_code(404);
    break;
}
