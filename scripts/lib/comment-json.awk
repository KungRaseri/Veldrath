# Decode a Forgejo comment list into "@@COMMENT <id>" followed by the decoded body.
#
# The API returns one line of JSON; bodies contain braces, quotes and newlines, so this
# walks the string character by character instead of splitting on '}' (which the old
# version of the comment script did and which breaks on any body containing one).
#
# Only the top-level object's "id" and "body" fields are kept; nested user/author
# objects are skipped by depth.
function hexval(hex,    i, digit, value) {
    value = 0
    hex = tolower(hex)
    for (i = 1; i <= 4; i++) {
        digit = index("0123456789abcdef", substr(hex, i, 1)) - 1
        if (digit < 0) { digit = 0 }
        value = value * 16 + digit
    }
    return value
}

function utf8(cp) {
    if (cp < 128) { return sprintf("%c", cp) }
    if (cp < 2048) { return sprintf("%c%c", 192 + int(cp / 64), 128 + cp % 64) }
    return sprintf("%c%c%c", 224 + int(cp / 4096), 128 + int(cp / 64) % 64, 128 + cp % 64)
}

{
    line = $0
    n = length(line)
    depth = 0
    i = 1
    id = ""
    body = ""
    key = ""
    expect_key = 0

    while (i <= n) {
        c = substr(line, i, 1)

        if (c == "\"") {
            i++
            s = ""
            while (i <= n) {
                c = substr(line, i, 1)
                if (c == "\\") {
                    e = substr(line, i + 1, 1)
                    i += 2
                    if (e == "n") s = s "\n"
                    else if (e == "t") s = s "\t"
                    else if (e == "r") { }
                    else if (e == "u") {
                        s = s utf8(hexval(substr(line, i, 4)))
                        i += 4
                    }
                    else s = s e
                } else if (c == "\"") {
                    i++
                    break
                } else {
                    s = s c
                    i++
                }
            }

            if (depth == 1) {
                if (expect_key) {
                    key = s
                    expect_key = 0
                } else if (key == "id") {
                    id = s
                    key = ""
                } else if (key == "body") {
                    body = s
                    key = ""
                }
            }
            continue
        }

        if (c == "{") {
            depth++
            if (depth == 1) { expect_key = 1 }
            i++
            continue
        }

        if (c == "}") {
            depth--
            if (depth == 0) {
                printf "@@COMMENT %s\n%s\n", id, body
                id = ""
                body = ""
                key = ""
                expect_key = 1
            }
            i++
            continue
        }

        if (c == ":") { i++; continue }

        if (c == ",") {
            if (depth == 1) { expect_key = 1 }
            key = ""
            i++
            continue
        }

        j = i
        while (j <= n && substr(line, j, 1) !~ /[:,{}]/) { j++ }
        token = substr(line, i, j - i)
        gsub(/[ \t]/, "", token)
        if (depth == 1 && expect_key && token != "") {
            key = token
            expect_key = 0
        } else if (depth == 1 && !expect_key && key == "id" && token != "") {
            id = token
            key = ""
        }
        i = j
    }
}
