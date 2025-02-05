import os
import sys

sys.stdout.reconfigure(encoding='utf-8')

from flask import Flask
from flask import request
from flask_cors import CORS, cross_origin

import time
import json
import re
import argparse

from functools import lru_cache
from logging import getLogger
from logging.config import dictConfig

dictConfig({
    'version': 1,
    'formatters': {'default': {
        'format': 'SugoiSrv[%(levelname)s] %(message)s',
    }},
    'handlers': {'wsgi': {
        'class': 'logging.StreamHandler',
        'stream': 'ext://sys.stdout',
        'formatter': 'default'
    }},
    'root': {
        'level': 'INFO',
        'handlers': ['wsgi']
    }
})


LOG = getLogger("root")


class TranslateBackendBase:
    def translate(self, s):
        raise NotImplementedError()


class Ctranslate2TranslateBackend(TranslateBackendBase):
    def __init__(self, settings):
        import sentencepiece as spm
        import ctranslate2

        spm_model_dir = os.path.join(settings.model_data_dir, '..', 'spmModels')

        self.source_spm = spm.SentencePieceProcessor(os.path.join(spm_model_dir, "spm.ja.nopretok.model"))
        self.target_spm = spm.SentencePieceProcessor(os.path.join(spm_model_dir, "spm.en.nopretok.model"))

        device = "cuda" if settings.cuda else "cpu"
        LOG.info(f"Setting up ctranslate2 translation backend using device {device}")
        self.translator = ctranslate2.Translator(
            model_path=settings.model_data_dir,
            device=device)

    def translate(self, s):
        line = self.source_spm.encode(s, out_type=str)
        LOG.info(f'translating: {line}')
        results = self.translator.translate_batch(
            [line],
            beam_size=5,
            num_hypotheses=1,
            no_repeat_ngram_size=3)
        return self.target_spm.decode(results[0].hypotheses)[0]


ja2en = None

app = Flask(__name__)

cors = CORS(app)
app.config['CORS_HEADERS'] = 'Content-Type'

@app.route("/", methods = ['POST'])
@cross_origin()
def sendImage():
    tic = time.perf_counter()
    data = request.get_json()
    message = data.get("message")
    content = data.get("content").strip('﻿').strip();

    if (message == "close server"):
        shutdown_server()
        return

    if (message == "translate sentences"):
        t = translate(content)

        toc = time.perf_counter()
        # LOG.info(f"Request: {content}")
        LOG.info(f"Translation {round(toc-tic,2)}s): {t}")

        return json.dumps(t)

    if (message == "translate batch"):
        batch = data.get("batch")
        if isinstance(batch, list):
            batch = [s.strip('﻿').strip() for s in batch]

            translated = [
                translate(s)
                for s in batch
            ]

        toc = time.perf_counter()
        # LOG.info(f"Request: {batch}")
        LOG.info(f"Translation complete {round(toc-tic,2)}s)")

        return json.dumps(translated)


def shutdown_server():
    func = request.environ.get('werkzeug.server.shutdown')
    if func is None:
        raise RuntimeError('Not running with the Werkzeug Server')
    func()


JP_TEXT_PATTERN = re.compile("[\u3040-\u30ff\u3400-\u4dbf\u4e00-\u9fff\uf900-\ufaff\uff66-\uff9f]")
PLACEHOLDER_PATTERN = re.compile(r"ZM(?P<word>[A-Z])Z")


@lru_cache
def translate(content):
    if not JP_TEXT_PATTERN.search(content):
        LOG.warn(f"Content [{content}] does not seem to have jp characters, skipping translation")
        return content

    filter_line, isBracket, isPeriod, replacements = pre_translate_filter(content)

    result = ja2en.translate(filter_line)
    result = restore_placeholders(result, replacements)
    result = post_translate_filter(result)
    
    if result.endswith(".") and not result.endswith("...") and not isPeriod and not isBracket:
        result = result[:-1]
    
    result = add_double_quote(result, isBracket)
    
    LOG.info(f"{content} => {result}")
    return result


def filter_placeholders(text):
    replacements = []
    old = text

    for match in PLACEHOLDER_PATTERN.finditer(text):
        word = match.group("word")
        whole = match.group()
        replacement = f"@#{word}"
        if replacement not in text:
            replacements.append((whole, replacement))

    for whole, replacement in replacements:
        text = text.replace(whole, replacement)

    if replacements:    
        LOG.info(f"Replaced placholders:[{old}] => [{text}]")

    return text, replacements


def restore_placeholders(text, replacements):
    for whole, replacement in replacements:
        text = text.replace(replacement, whole)
    return text


def pre_translate_filter(data):
    # data = data.replace('\n', '')
    # data = data.replace('\u3000', '')  # remove "　"
    # data = data.replace('\u200b', '')
    data = data.strip()

    isBracket = data.endswith("」") and data.startswith('「')
    isPeriod = data.endswith("。")
    data, replacements = filter_placeholders(data)

    return data, isBracket, isPeriod, replacements


def post_translate_filter(data):
    text = data
    text = text.replace('<unk>', ' ')
    # text = text.replace('―', '-')
    
    start = text[0]
    end = text[-1]

    start_quotes = ('「', '”', '“', '"', "'")
    end_quotes = ('」',  '“', '”', '"', "'")

    if start in start_quotes:
        text = text[1:]

    if end in end_quotes:
        text = text[:-1]

    text = text.strip()
    text = text[0].upper() + text[1:]

    return text


def add_double_quote(data, isBracket):
    en_text = data
    if isBracket:
        en_text = f'"{data}"'

    return en_text


def parse_commandline_args():
    parser = argparse.ArgumentParser(description="SugoiOfflineTranslator backend server")
    parser.add_argument('port', type=int,
                        help="The port to listen to")
    parser.add_argument('--cuda', action="store_true",
                        help="Run translations on the GPU via CUDA")
    parser.add_argument('--model-data-dir', type=str, default=None,
                        help="CT2 model directory")

    return parser.parse_args()


def main():
    global ja2en

    args = parse_commandline_args()

    if args.model_data_dir is None:
        for root in ("models", "Sugoi_Model"):
            test_dir = os.path.join(".", root, "ct2Model")
            if os.path.exists(test_dir):
                args.model_data_dir = test_dir
                break
    if args.model_data_dir is None:
        LOG.error(f"Unable to find valid model data under {os.getcwd()}")
        sys.exit(2)
    if not os.path.exists(args.model_data_dir):
        LOG.error(f"model data dir does not exist: {args.model_data_dir}")
        sys.exit(3)
    # monkey patch cli banner
    from flask import cli
    cli.show_server_banner = lambda *_: None

    ja2en = Ctranslate2TranslateBackend(args)

    LOG.info(f"Running server on port {args.port}")
    LOG.info(f" * model data dir '{args.model_data_dir}'")
    app.run(host='127.0.0.1', port=args.port)


if __name__ == "__main__":
    main()

