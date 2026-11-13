-- Фабрика светопрозрачных конструкций (вариант 16, ПР-23, ОКН)
-- Импорт: phpMyAdmin (http://localhost/phpmyadmin/) -> вкладка «Импорт» -> выбрать этот файл.
-- Демо-пароли: technolog / technolog2 -> Tech#2024; manager, manager2..4 -> Manager#2024 (хранится SHA256).
SET NAMES utf8mb4;
SET FOREIGN_KEY_CHECKS = 0;
CREATE DATABASE IF NOT EXISTS window_factory CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE window_factory;
DROP TABLE IF EXISTS window_order_units;
DROP TABLE IF EXISTS window_orders;
DROP TABLE IF EXISTS homeowners;
DROP TABLE IF EXISTS glazing_types;
DROP TABLE IF EXISTS profile_systems;
DROP TABLE IF EXISTS users;
DROP TABLE IF EXISTS roles;
SET FOREIGN_KEY_CHECKS = 1;

CREATE TABLE roles (
  id INT AUTO_INCREMENT PRIMARY KEY,
  name VARCHAR(100) NOT NULL,
  CONSTRAINT uq_roles_name UNIQUE (name)
) ENGINE=InnoDB;

CREATE TABLE users (
  id INT AUTO_INCREMENT PRIMARY KEY,
  role_id INT NOT NULL,
  login VARCHAR(50) NOT NULL,
  password_hash CHAR(64) NOT NULL COMMENT 'SHA256, hex',
  full_name VARCHAR(150) NOT NULL,
  CONSTRAINT uq_users_login UNIQUE (login),
  CONSTRAINT fk_users_role FOREIGN KEY (role_id) REFERENCES roles(id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB;

CREATE TABLE profile_systems (
  id INT AUTO_INCREMENT PRIMARY KEY,
  brand_title VARCHAR(100) NOT NULL,
  chambers_count INT NOT NULL,
  frame_depth_mm INT NOT NULL,
  price_per_meter DECIMAL(10,2) NOT NULL,
  CONSTRAINT ck_profile_chambers CHECK (chambers_count > 0),
  CONSTRAINT ck_profile_depth CHECK (frame_depth_mm > 0),
  CONSTRAINT ck_profile_price CHECK (price_per_meter >= 0)
) ENGINE=InnoDB;

CREATE TABLE glazing_types (
  id INT AUTO_INCREMENT PRIMARY KEY,
  glazing_name VARCHAR(100) NOT NULL,
  thickness_mm INT NOT NULL,
  energy_saving TINYINT(1) NOT NULL DEFAULT 0,
  price_per_m2 DECIMAL(10,2) NOT NULL,
  CONSTRAINT ck_glazing_thickness CHECK (thickness_mm > 0),
  CONSTRAINT ck_glazing_price CHECK (price_per_m2 >= 0)
) ENGINE=InnoDB;

CREATE TABLE homeowners (
  id INT AUTO_INCREMENT PRIMARY KEY,
  full_name VARCHAR(150) NOT NULL,
  delivery_address VARCHAR(250) NOT NULL,
  floor_number INT NOT NULL DEFAULT 1,
  has_elevator TINYINT(1) NOT NULL DEFAULT 0 COMMENT 'есть ли лифт (для надбавки за подъём)',
  phone VARCHAR(20) NOT NULL,
  CONSTRAINT ck_homeowner_floor CHECK (floor_number >= 1)
) ENGINE=InnoDB;

CREATE TABLE window_orders (
  id INT AUTO_INCREMENT PRIMARY KEY,
  order_num VARCHAR(20) NOT NULL COMMENT 'ПР23-ОКН-{год}-{номер:D5}',
  user_id INT NOT NULL,
  homeowner_id INT NOT NULL,
  order_date DATETIME NOT NULL,
  has_installation TINYINT(1) NOT NULL DEFAULT 0,
  total_price DECIMAL(12,2) NOT NULL DEFAULT 0,
  status VARCHAR(30) NOT NULL DEFAULT 'Замер выполнен',
  cancel_reason VARCHAR(500) NULL,
  completed_at DATETIME NULL COMMENT 'дата сдачи, от неё считается гарантия 36 мес.',
  CONSTRAINT uq_orders_num UNIQUE (order_num),
  CONSTRAINT ck_orders_total CHECK (total_price >= 0),
  CONSTRAINT fk_orders_user FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT fk_orders_homeowner FOREIGN KEY (homeowner_id) REFERENCES homeowners(id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB;

CREATE TABLE window_order_units (
  id INT AUTO_INCREMENT PRIMARY KEY,
  order_id INT NOT NULL,
  profile_system_id INT NOT NULL,
  glazing_type_id INT NOT NULL,
  width_mm INT NOT NULL,
  height_mm INT NOT NULL,
  sashes_count INT NOT NULL,
  unit_calculated_cost DECIMAL(12,2) NOT NULL,
  CONSTRAINT ck_unit_width CHECK (width_mm BETWEEN 400 AND 1500),
  CONSTRAINT ck_unit_height CHECK (height_mm BETWEEN 400 AND 2200),
  CONSTRAINT ck_unit_sashes CHECK (sashes_count BETWEEN 1 AND 6),
  CONSTRAINT ck_unit_cost CHECK (unit_calculated_cost >= 0),
  CONSTRAINT fk_units_order FOREIGN KEY (order_id) REFERENCES window_orders(id) ON DELETE CASCADE ON UPDATE CASCADE,
  CONSTRAINT fk_units_profile FOREIGN KEY (profile_system_id) REFERENCES profile_systems(id) ON DELETE RESTRICT ON UPDATE CASCADE,
  CONSTRAINT fk_units_glazing FOREIGN KEY (glazing_type_id) REFERENCES glazing_types(id) ON DELETE RESTRICT ON UPDATE CASCADE
) ENGINE=InnoDB;

INSERT INTO roles (id, name) VALUES
  (1, 'Главный технолог'),
  (2, 'Менеджер по замерам и продажам');

INSERT INTO users (id, role_id, login, password_hash, full_name) VALUES
  (1, 1, 'technolog', '697757fb8a332c6a930349dbf7b563ce9cc36f6c973272326628b495a7cad7a7', 'Воронцов Игорь Сергеевич'),
  (2, 1, 'technolog2', '697757fb8a332c6a930349dbf7b563ce9cc36f6c973272326628b495a7cad7a7', 'Левченко Марина Андреевна'),
  (3, 2, 'manager', '7b6ce31ec7047c4bd0e3406b3f0310f6a6c0f89c4455c6e5b2d296626427222a', 'Смирнова Анна Павловна'),
  (4, 2, 'manager2', '7b6ce31ec7047c4bd0e3406b3f0310f6a6c0f89c4455c6e5b2d296626427222a', 'Кузнецов Дмитрий Олегович'),
  (5, 2, 'manager3', '7b6ce31ec7047c4bd0e3406b3f0310f6a6c0f89c4455c6e5b2d296626427222a', 'Фёдорова Елена Викторовна'),
  (6, 2, 'manager4', '7b6ce31ec7047c4bd0e3406b3f0310f6a6c0f89c4455c6e5b2d296626427222a', 'Назаров Артём Ильич');

INSERT INTO profile_systems (id, brand_title, chambers_count, frame_depth_mm, price_per_meter) VALUES
  (1, 'Rehau Blitz 60', 3, 60, 540),
  (2, 'Rehau Delight Design 70', 5, 70, 690),
  (3, 'Rehau Grazio 70', 6, 70, 780),
  (4, 'Veka Euroline 58', 3, 58, 520),
  (5, 'Veka Softline 70', 5, 70, 720),
  (6, 'Veka Softline 82', 6, 82, 880),
  (7, 'Provedal C640 (алюминий)', 3, 64, 1150);

INSERT INTO glazing_types (id, glazing_name, thickness_mm, energy_saving, price_per_m2) VALUES
  (1, 'Однокамерный 24 мм', 24, 0, 1450),
  (2, 'Однокамерный энергосберегающий 24 мм', 24, 1, 1750),
  (3, 'Двухкамерный 32 мм', 32, 0, 2100),
  (4, 'Двухкамерный энергосберегающий 32 мм', 32, 1, 2550),
  (5, 'Двухкамерный энергосберегающий 40 мм', 40, 1, 2900),
  (6, 'Трёхкамерный 44 мм', 44, 1, 3300);

INSERT INTO homeowners (id, full_name, delivery_address, floor_number, has_elevator, phone) VALUES
  (1, 'Иванов Пётр Алексеевич', 'г. Москва, ул. Ленина, д. 12, кв. 15', 3, 0, '+7 (916) 123-45-67'),
  (2, 'Петрова Ольга Николаевна', 'г. Москва, ул. Садовая, д. 8, кв. 54', 9, 0, '+7 (903) 222-33-44'),
  (3, 'Сидоров Андрей Викторович', 'г. Химки, ул. Мира, д. 3, кв. 71', 7, 1, '+7 (925) 555-12-34'),
  (4, 'Морозова Татьяна Игоревна', 'г. Балашиха, пр. Ленина, д. 25, кв. 120', 12, 0, '+7 (977) 810-20-30'),
  (5, 'Волков Сергей Михайлович', 'г. Мытищи, ул. Колпакова, д. 41, кв. 6', 2, 0, '8 (905) 444-55-66'),
  (6, 'Лебедева Ирина Павловна', 'г. Москва, Каширское ш., д. 61, кв. 88', 8, 0, '+7 (999) 100-20-30'),
  (7, 'Кузьмин Алексей Романович', 'г. Королёв, ул. Гагарина, д. 5, кв. 2', 1, 0, '+7 (915) 777-88-99'),
  (8, 'Орлова Наталья Сергеевна', 'г. Люберцы, ул. Победы, д. 19, кв. 33', 6, 0, '+7 (926) 300-40-50');

INSERT INTO window_orders (id, order_num, user_id, homeowner_id, order_date, has_installation, total_price, status, cancel_reason, completed_at) VALUES
  (1, 'ПР23-ОКН-2023-00001', 3, 1, '2023-09-12 10:15:00', 1, 27652.80, 'Смонтировано', NULL, '2023-10-05 14:00:00'),
  (2, 'ПР23-ОКН-2023-00002', 4, 2, '2023-11-01 11:30:00', 1, 27898.80, 'Смонтировано', NULL, '2023-12-01 16:30:00'),
  (3, 'ПР23-ОКН-2025-00001', 3, 3, '2025-02-20 09:45:00', 1, 30064.80, 'Смонтировано', NULL, '2025-03-10 13:00:00'),
  (4, 'ПР23-ОКН-2025-00002', 5, 4, '2025-06-15 12:00:00', 1, 52968.00, 'Рекламация', 'Трещина в стеклопакете при монтаже, требуется замена', '2025-07-20 15:00:00'),
  (5, 'ПР23-ОКН-2026-00001', 4, 5, '2026-09-02 10:00:00', 0, 13873.00, 'Изготовлено', NULL, NULL),
  (6, 'ПР23-ОКН-2026-00002', 3, 6, '2026-10-10 14:20:00', 1, 44823.60, 'Передано в цех', NULL, NULL),
  (7, 'ПР23-ОКН-2026-00003', 6, 7, '2026-10-28 09:10:00', 0, 18232.00, 'Замер выполнен', NULL, NULL),
  (8, 'ПР23-ОКН-2026-00004', 5, 8, '2026-11-05 17:40:00', 1, 43590.30, 'Замер выполнен', NULL, NULL);

INSERT INTO window_order_units (id, order_id, profile_system_id, glazing_type_id, width_mm, height_mm, sashes_count, unit_calculated_cost) VALUES
  (1, 1, 1, 3, 1300, 1400, 2, 11738.00),
  (2, 1, 1, 3, 900, 1200, 1, 7036.00),
  (3, 1, 4, 1, 600, 600, 1, 4270.00),
  (4, 2, 2, 4, 1400, 1500, 2, 14357.00),
  (5, 2, 2, 4, 800, 1400, 1, 8392.00),
  (6, 3, 5, 4, 1500, 1400, 3, 17031.00),
  (7, 3, 5, 4, 700, 1400, 1, 8023.00),
  (8, 4, 3, 5, 1200, 1300, 2, 13424.00),
  (9, 4, 3, 5, 1000, 1300, 1, 9858.00),
  (10, 4, 7, 6, 1100, 2100, 2, 19983.00),
  (11, 5, 4, 3, 1000, 1200, 2, 9808.00),
  (12, 5, 4, 3, 500, 500, 1, 4065.00),
  (13, 6, 6, 5, 1400, 1450, 2, 15903.00),
  (14, 6, 6, 5, 1400, 1450, 2, 15903.00),
  (15, 6, 6, 2, 400, 800, 1, 5172.00),
  (16, 7, 1, 1, 1200, 1000, 2, 9116.00),
  (17, 7, 1, 1, 1200, 1000, 2, 9116.00),
  (18, 8, 2, 4, 1300, 1300, 2, 12897.50),
  (19, 8, 2, 4, 1300, 1300, 2, 12897.50),
  (20, 8, 2, 4, 600, 1000, 1, 6238.00),
  (21, 8, 2, 3, 450, 450, 1, 4167.25);
