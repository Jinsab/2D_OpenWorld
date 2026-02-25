<?php
// Basic installer script for Crystal Save's MySQL backend.
// Fill in your own credentials below and run this file once to
// create the required table.

// ── Config ───────────────────────────────────────────────────────
$db_host = '127.0.0.1';
$db_name = 'game';
$db_user = 'unityuser';
$db_pass = 'ChangeMe!';
$table   = 'CrystalSaveData';

// ── Connect and create table ─────────────────────────────────────
try {
    $pdo = new PDO(
        "mysql:host=$db_host;dbname=$db_name;charset=utf8mb4",
        $db_user, $db_pass,
        [PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION]
    );

    $sql = "CREATE TABLE IF NOT EXISTS `$table` (
        UserID             VARCHAR(64) NOT NULL,
        Slot               INT         NOT NULL,
        Data               MEDIUMBLOB  NOT NULL,
        SlotName           VARCHAR(64),
        LastSavedTicks     BIGINT,
        LastActiveScene    VARCHAR(64),
        ScreenshotFileName VARCHAR(255),
        CustomMetadata     JSON,
        PRIMARY KEY (UserID, Slot)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

    $pdo->exec($sql);
    echo 'Table created or already exists.';
} catch (PDOException $e) {
    http_response_code(500);
    echo 'Installation failed: ' . $e->getMessage();
}
