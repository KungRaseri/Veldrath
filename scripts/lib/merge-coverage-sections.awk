# Merge one named section into a sticky summary comment.
#
# Input:  an existing comment body on stdin (may be empty).
# Output: the body with section `section` replaced (or appended), every other section
#         kept, and a stable order: dotnet, web, then anything else in first-seen order.
#
# Idempotent: re-merging the same body and content produces byte-identical output, so a
# retry after a lost race cannot accumulate blank lines or duplicate headings.
#
# Requires -v section=NAME -v title=TEXT -v bodyfile=PATH.
# Optional -v marker=HTML_COMMENT and -v heading=MARKDOWN. The defaults are the coverage
# comment, so every existing caller keeps its exact bytes; the e2e reporter passes its own
# marker (`<!-- ci-e2e -->`) and heading (`## E2E summary`) to reuse this merge instead of
# growing a second implementation of it.
BEGIN {
    header = marker
    if (header == "") header = "<!-- ci-coverage -->"
    heading_line = heading
    if (heading_line == "") heading_line = "## Coverage summary"
    count = 0
    in_section = 0
    current = ""
}

{
    if (in_section) {
        if ($0 == "<!-- /coverage-section:" current " -->") {
            content[current] = trim(content[current])
            in_section = 0
            current = ""
            next
        }
        content[current] = content[current] $0 "\n"
        next
    }

    if ($0 ~ /^<!-- coverage-section:[^ ]+ -->$/) {
        name = $0
        sub(/^<!-- coverage-section:/, "", name)
        sub(/ -->$/, "", name)
        current = name
        in_section = 1
        content[name] = ""
        if (!(name in seen)) {
            seen[name] = 1
            order[++count] = name
        }
        next
    }

    # Everything outside a section (the shared header, the old H2, stray blank lines)
    # is regenerated; nothing else is expected there.
}

END {
    if (!(section in seen)) {
        seen[section] = 1
        order[++count] = section
        content[section] = ""
    }

    print header
    print heading_line

    canonical[1] = "dotnet"
    canonical[2] = "web"

    for (k = 1; k <= 2; k++) {
        if (canonical[k] in seen) { emit_section(canonical[k]) }
    }
    for (k = 1; k <= count; k++) {
        name = order[k]
        if (name == "dotnet" || name == "web") { continue }
        if (name in seen) { emit_section(name) }
    }
}

function trim(s) {
    sub(/^[ \t]*\n+/, "", s)
    sub(/\n+[ \t]*$/, "", s)
    return s
}

function emit_section(name,    line, body) {
    print ""
    print "<!-- coverage-section:" name " -->"
    print ""
    if (name == section) {
        print "### " title
        print ""
        body = ""
        while ((getline line < bodyfile) > 0) {
            # The reporter's own copy of the heading and marker is dropped: the heading above is
            # generated here, from -v heading, so keeping the body's would print it twice.
            if (line == heading_line || line == header) { continue }
            body = body line "\n"
        }
        close(bodyfile)
        printf "%s\n", trim(body)
    } else {
        printf "%s\n", content[name]
    }
    print ""
    print "<!-- /coverage-section:" name " -->"
}
