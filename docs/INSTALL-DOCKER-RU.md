# Library Collage RU 1.0.11.100 для Jellyfin Docker

## Перед заменой

Тестовая сборка заменяет Library Collage 1.0.11.0. Цель — Jellyfin **12.4.0**, API/ABI **12.4.0.0**, .NET **10**. Управляемая DLL платформонезависима. SkiaSharp 3.119.4 и соответствующую нативную библиотеку предоставляет Jellyfin. На хосте нужны команды Docker, GNU tar, unzip и sha256sum. Используйте учётную запись с доступом к Docker и правом читать и записывать каталог конфигурации.

У форка и оригинала общий GUID `0f93e2db-b456-4e09-a09a-76154a40aed6`, имя сборки `Jellyfin.Plugin.LibraryCollage` и API-маршруты. Сохраните прежний XML и хранилище `managed-images.json`. Запускайте только один экземпляр. В комплектном `meta.json` отключено автообновление. В панели Jellyfin удалите отдельный репозиторий Library Collage от digital-workshop, если он был добавлен, чтобы исключить случайную установку оригинала из каталога. Не нажимайте Uninstall.

1. Скопируйте ZIP и `SHA256SUMS-install.txt` на сервер в один каталог и откройте его в SSH. Выполните `sha256sum -c SHA256SUMS-install.txt`, ожидается `OK`. Для развёртывания используйте абсолютный путь к архиву.
2. Проверьте версию в панели Jellyfin и точку монтирования.

   ```sh
   docker inspect --format '{{range .Mounts}}{{if eq .Destination "/config"}}{{println .Type .Source .Destination .RW}}{{end}}{{end}}' jellyfin
   ```

   Ожидается `bind /mnt/elements/docker/jellyfin/config /config true`. Если путь или тип отличаются, адаптируйте процедуру к фактическому хранилищу. Сверьте пути к изображениям, если используете отдельные монтирования для metadata или root, и включите их в резервную копию перед генерацией.

3. В той же SSH-сессии задайте пути и найдите действующий каталог плагина.

   ```sh
   cfg=/mnt/elements/docker/jellyfin/config; zip="$PWD/library-collage-ru_1.0.11.100.zip"; backup="$HOME/jellyfin-library-collage-$(date +%Y%m%d-%H%M%S)"; next="$cfg/plugins/Library Collage_1.0.11.100"
   find "$cfg/plugins" -maxdepth 2 -name Jellyfin.Plugin.LibraryCollage.dll -print
   ```

   Убедитесь, что найден ровно один экземпляр. Подставьте фактический путь в `old`, если установка назвала каталог иначе. Проверьте свободное место для резервной копии всего `/config`.

   ```sh
   old="$cfg/plugins/Library Collage_1.0.11.0"; test -f "$old/Jellyfin.Plugin.LibraryCollage.dll" && test -f "$zip" && test ! -e "$next" && du -sh "$cfg" && df -h "$cfg" "$HOME"
   ```

## Установка

Выполняйте по одной строке. Если команда завершилась с ошибкой, остановитесь и проверьте состояние.

1. Остановите Jellyfin и сделайте резервную копию. Архив включает базу, настройки, XML плагина, каталог плагинов и изображения, расположенные под `/config`.

   ```sh
   mkdir -m 700 -- "$backup" && docker stop jellyfin
   tar --acls --xattrs -cpf "$backup/config.tar" -C "$cfg" . && tar -tf "$backup/config.tar" >/dev/null
   ```

2. Подготовьте замену. Копирование прежней папки сохраняет данные, если они находятся рядом с DLL. Распаковывайте только проверенный ZIP. Переместите оригинал за пределы `plugins`, затем установите подготовленную папку.

   ```sh
   cp -a -- "$old" "$backup/staged" && unzip -o "$zip" -d "$backup/staged" && test -f "$backup/staged/meta.json" && test -f "$backup/staged/Jellyfin.Plugin.LibraryCollage.dll"
   mv -- "$old" "$backup/original" && mv -- "$backup/staged" "$next"
   docker start jellyfin
   docker logs --since 5m jellyfin 2>&1 | grep -Ei 'library.?collage|failed to load|FileLoadException|MissingMethodException'
   ```

   Проверьте журнал, а в Dashboard → Plugins убедитесь, что загружена версия `1.0.11.100`. Откройте настройки, выберите библиотеку, задайте шрифт, нажмите **Preview** и проверьте результат с темой Flow. Дополнительная проверка — после Preview закрыть и снова открыть настройки: на сервере должны остаться ранее сохранённые значения. Для пробной генерации выберите одну библиотеку и нажмите **Generate**. Включайте **Overwrite anyway** только для изображения, которое намеренно хотите заменить. Сохраните путь из переменной `backup`.

## Откат

Если выполнялся только Preview, верните оригинальный плагин. Подставьте сохранённое значение `backup` и исходные пути, если открыли новую SSH-сессию.

```sh
test -d "$backup/original" && test -d "$next" && test ! -e "$old" && test ! -e "$backup/fork" && docker stop jellyfin
mv -- "$next" "$backup/fork" && mv -- "$backup/original" "$old" && docker start jellyfin
```

Если сохраняли конфигурацию или перегенерировали изображения и нужно восстановить настройки, обложки и ссылки в базе, верните согласованный снимок всего `/config`. При таком откате состояние Jellyfin вернётся к моменту копирования, включая изменения других настроек после снимка. Этот вариант рассчитан на обычный каталог bind mount, у которого `$cfg` не является корнем файловой системы.

```sh
restore="$cfg.restore-$(date +%Y%m%d-%H%M%S)"; displaced="$restore.fork"; mkdir -- "$restore" && tar --acls --xattrs --same-permissions -xpf "$backup/config.tar" -C "$restore"
test -d "$restore/plugins" && test ! -e "$displaced" && docker stop jellyfin && mv -- "$cfg" "$displaced" && mv -- "$restore" "$cfg" && docker start jellyfin
```

Если второе переименование завершилось ошибкой, верните `"$displaced"` на место `"$cfg"` до запуска Docker. После отката проверьте в Dashboard версию **1.0.11.0** и обложки. При необходимости восстановите отдельно сохранённые монтирования с изображениями той же датой.
